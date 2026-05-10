# Stonks POC — Technical Implementation Plan

## Overview

This document describes the concrete implementation plan for the Stonks POC. The goal is a working end-to-end flow: user enters a ticker and date range → server fetches OHLCV from Finnhub → server sends data to Gemini for analysis → results stream back to the Avalonia desktop client via gRPC.

**Stack decisions:** Finnhub (market data), Gemini Flash (AI), gRPC streaming, file-based cache, DotNetEnv config.

---

## 1. Solution Structure

```
Stonks.sln
src/
├── Stonks.Shared/              # Protobuf contracts + shared C# types
├── Stonks.Server/              # ASP.NET Core gRPC server
└── Stonks.Client.Desktop/      # Avalonia desktop client
```

Create with:
```
dotnet new sln -n Stonks
dotnet new classlib -n Stonks.Shared      -o src/Stonks.Shared      -f net10.0
dotnet new web    -n Stonks.Server        -o src/Stonks.Server       -f net10.0
dotnet new avalonia.app -n Stonks.Client.Desktop -o src/Stonks.Client.Desktop
dotnet sln add src/Stonks.Shared src/Stonks.Server src/Stonks.Client.Desktop
```

---

## 2. NuGet Packages

### Stonks.Shared
| Package | Version | Purpose |
|---|---|---|
| `Google.Protobuf` | latest | Protobuf runtime |
| `Grpc.Tools` | latest | Build-time proto codegen |

### Stonks.Server
| Package | Version | Purpose |
|---|---|---|
| `Grpc.AspNetCore` | latest | gRPC server hosting |
| `DotNetEnv` | 3.1.1 | `.env` file loading |

`System.Text.Json` and `System.Net.Http` are included with .NET 10 — no extra packages needed.

### Stonks.Client.Desktop
| Package | Version | Purpose |
|---|---|---|
| `Avalonia` | 11.x | Cross-platform UI framework |
| `Avalonia.Desktop` | 11.x | Desktop runtime |
| `Avalonia.Themes.Fluent` | 11.x | Fluent theme |
| `Grpc.Net.Client` | latest | gRPC client |
| `Google.Protobuf` | latest | Protobuf runtime |
| `Grpc.Tools` | latest | Build-time proto codegen |
| `ScottPlot.Avalonia` | 5.x | Candlestick charting |

---

## 3. Protobuf Contract (`Stonks.Shared`)

**File:** `src/Stonks.Shared/Protos/stocks.proto`

```proto
syntax = "proto3";

option csharp_namespace = "Stonks.Shared.Grpc";

package stonks;

service StocksAnalysis {
  rpc AnalyzeStock (AnalyzeStockRequest) returns (stream AnalyzeStockResponse);
}

message AnalyzeStockRequest {
  string ticker     = 1;
  string start_date = 2;  // YYYY-MM-DD
  string end_date   = 3;  // YYYY-MM-DD
}

message AnalyzeStockResponse {
  oneof payload {
    OhlcvData   ohlcv_data     = 1;
    string      analysis_chunk = 2;
    string      error_message  = 3;
  }
}

message OhlcvBar {
  string date   = 1;
  double open   = 2;
  double high   = 3;
  double low    = 4;
  double close  = 5;
  int64  volume = 6;
}

message OhlcvData {
  string            ticker = 1;
  repeated OhlcvBar bars   = 2;
}
```

Add to `Stonks.Shared.csproj`:
```xml
<ItemGroup>
  <Protobuf Include="Protos/stocks.proto" GrpcServices="Both" />
</ItemGroup>
```

Both server and client reference `Stonks.Shared` so they share generated types.

---

## 4. Configuration (`.env`)

Place `.env` in the server project root (`src/Stonks.Server/.env`). Add to `.gitignore`.

Create an example `.env` called `.env.example` with placeholders.  This will be checked in and public so it must NOT contain any actual credentials.

```
FINNHUB_API_KEY=<from POC_1_CRED.md>
GEMINI_API_KEY=<from POC_1_CRED.md>
CACHE_AI_RESULTS=false
SERVER_PORT=5001
```

Load at server startup (before `builder.Build()`):
```csharp
DotNetEnv.Env.Load();
```

All config reads use `Environment.GetEnvironmentVariable("KEY")`.

---

## 5. Server Implementation (`Stonks.Server`)

### 5.1 Project Layout

```
src/Stonks.Server/
├── Program.cs
├── .env
├── Services/
│   └── StocksAnalysisService.cs    # gRPC service implementation
├── MarketData/
│   ├── IMarketDataClient.cs
│   └── FinnhubClient.cs
├── Ai/
│   ├── IAiClient.cs
│   └── GeminiClient.cs
└── Cache/
    ├── ICacheService.cs
    └── FileCacheService.cs
```

### 5.2 Cache Service

**`ICacheService.cs`**
```csharp
public interface ICacheService
{
    bool TryGet<T>(string key, out T? value);
    void Set<T>(string key, T value, TimeSpan? ttl = null);
    void Invalidate(string key);
}
```

**`FileCacheService.cs`** — Key implementation details:
- Base directory resolved via `Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)` + `Stonks/cache/`
- Cache entry written as JSON: `{ "value": <T>, "expiresAt": "<ISO8601 or null>" }`
- Key sanitized to a safe filename (replace non-alphanumeric with `_`)
- `TryGet` checks `expiresAt` and deletes stale file if expired

### 5.3 Finnhub Market Data Client

**`IMarketDataClient.cs`**
```csharp
public interface IMarketDataClient
{
    Task<IReadOnlyList<OhlcvBar>> GetOhlcvAsync(
        string ticker, DateOnly startDate, DateOnly endDate,
        CancellationToken ct = default);
}
```

**`FinnhubClient.cs`** — Key implementation details:
- Base URL: `https://finnhub.io/api/v1/stock/candle`
- Query params: `symbol`, `resolution=D`, `from` (Unix timestamp), `to` (Unix timestamp), `token`
- Convert `DateOnly` to Unix seconds via `DateTimeOffset`
- Deserialize parallel arrays `c[]`, `h[]`, `l[]`, `o[]`, `t[]`, `v[]` into `OhlcvBar` list
- Cache key: `{TICKER.ToUpper()}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}`
- TTL: `null` (indefinite) if `endDate < DateOnly.FromDateTime(DateTime.UtcNow)`, else 1 hour

Finnhub response shape:
```json
{ "c": [], "h": [], "l": [], "o": [], "s": "ok", "t": [], "v": [] }
```

### 5.4 Gemini AI Client

**`IAiClient.cs`**
```csharp
public interface IAiClient
{
    IAsyncEnumerable<string> AnalyzeAsync(
        string ticker, IReadOnlyList<OhlcvBar> bars,
        CancellationToken ct = default);
}
```

**`GeminiClient.cs`** — Key implementation details:
- Streaming endpoint: `POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:streamGenerateContent?key={apiKey}`
- Auth via `key` query parameter
- Build prompt as plain text table of OHLCV data with a system instruction asking for technical analysis
- Parse SSE response: each `data:` line is a JSON chunk; extract `candidates[0].content.parts[0].text`
- If `CACHE_AI_RESULTS=true`, cache the full assembled response using key `{TICKER}_{start}_{end}_{promptHash}`; on cache hit, yield the cached string as a single chunk

**Prompt template:**
```
You are an expert technical analyst. Analyze the following daily OHLCV data for {ticker} 
and provide a concise technical analysis covering: trend direction, key support/resistance 
levels, notable patterns, and a brief outlook.

Ticker: {ticker}
Date Range: {startDate} to {endDate}

Date,Open,High,Low,Close,Volume
{csv rows}

Provide your analysis in clear, structured paragraphs.
```

### 5.5 gRPC Service

**`StocksAnalysisService.cs`**
```csharp
public class StocksAnalysisService : StocksAnalysis.StocksAnalysisBase
{
    public override async Task AnalyzeStock(
        AnalyzeStockRequest request,
        IServerStreamWriter<AnalyzeStockResponse> responseStream,
        ServerCallContext context)
    {
        // 1. Fetch OHLCV (cache-aware)
        var bars = await marketDataClient.GetOhlcvAsync(...);

        // 2. Send OHLCV data to client first so chart renders immediately
        await responseStream.WriteAsync(new AnalyzeStockResponse
        {
            OhlcvData = new OhlcvData { Ticker = request.Ticker, Bars = { bars } }
        });

        // 3. Stream AI analysis chunks
        await foreach (var chunk in aiClient.AnalyzeAsync(request.Ticker, bars, context.CancellationToken))
        {
            await responseStream.WriteAsync(new AnalyzeStockResponse
            {
                AnalysisChunk = chunk
            });
        }
    }
}
```

### 5.6 Program.cs (Server Bootstrap)

```csharp
DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    var port = int.Parse(Environment.GetEnvironmentVariable("SERVER_PORT") ?? "5001");
    options.ListenLocalhost(port, o => o.Protocols = HttpProtocols.Http2);
});

builder.Services.AddGrpc();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ICacheService, FileCacheService>();
builder.Services.AddSingleton<IMarketDataClient, FinnhubClient>();
builder.Services.AddSingleton<IAiClient, GeminiClient>();

var app = builder.Build();
app.MapGrpcService<StocksAnalysisService>();
app.Run();
```

---

## 6. Desktop Client Implementation (`Stonks.Client.Desktop`)

### 6.1 Project Layout

```
src/Stonks.Client.Desktop/
├── Program.cs
├── App.axaml / App.axaml.cs
├── ViewModels/
│   └── MainWindowViewModel.cs
└── Views/
    └── MainWindow.axaml / MainWindow.axaml.cs
```

### 6.2 gRPC Channel Setup

```csharp
// Allow HTTP/2 without TLS for local dev
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
var channel = GrpcChannel.ForAddress("http://localhost:5001");
var client  = new StocksAnalysis.StocksAnalysisClient(channel);
```

Register as a singleton via Avalonia's DI container or pass through the ViewModel constructor.

### 6.3 MainWindowViewModel

Key observable properties:
```csharp
public string Ticker            { get; set; }
public DateTimeOffset StartDate { get; set; }
public DateTimeOffset EndDate   { get; set; }
public string AnalysisText      { get; set; }   // appended as chunks arrive
public bool IsLoading           { get; set; }
public ICommand AnalyzeCommand  { get; }
public OhlcvBar[] ChartBars     { get; set; }   // bound to ScottPlot
```

`AnalyzeCommand` implementation:
1. Set `IsLoading = true`, clear `AnalysisText`
2. Open gRPC streaming call: `client.AnalyzeStock(new AnalyzeStockRequest { ... })`
3. Iterate `responseStream.ResponseStream.ReadAllAsync()`:
   - On `OhlcvData` payload → populate `ChartBars`
   - On `AnalysisChunk` payload → append to `AnalysisText` on UI thread
4. Set `IsLoading = false` on completion or error

### 6.4 MainWindow Layout (AXAML)

```
┌─────────────────────────────────────────────────────────┐
│  [Ticker: AAPL]  [Start: 2024-01-01]  [End: 2024-06-30] │  ← Input row
│                          [Analyze]                       │
├─────────────────────────────┬───────────────────────────┤
│                             │                           │
│   Candlestick Chart         │   AI Analysis Text        │
│   (ScottPlot.Avalonia)      │   (ScrollViewer +         │
│                             │    TextBlock, streaming)  │
│                             │                           │
├─────────────────────────────┴───────────────────────────┤
│  ⚠ For educational purposes only. Not financial advice. │  ← Disclaimer
└─────────────────────────────────────────────────────────┘
```

Layout elements:
- `Grid` with 3 rows: input, content, disclaimer
- Input row: `StackPanel` (Horizontal) with `TextBox`, two `DatePicker`s, `Button`
- Content row: `Grid` with 2 columns — `AvaPlot` (left), `ScrollViewer > TextBlock` (right)
- Disclaimer: `Border` with light background, `TextBlock` centered

### 6.5 Chart Integration (ScottPlot)

```csharp
// In code-behind or via binding after ChartBars is populated
var candlestickPlot = AvaPlot.Plot;
candlestickPlot.Clear();
var ohlcList = bars.Select(b => new OHLC(b.Open, b.High, b.Low, b.Close,
    DateTime.Parse(b.Date), TimeSpan.FromDays(1))).ToArray();
candlestickPlot.Add.Candlestick(ohlcList);
candlestickPlot.Axes.DateTimeTicksBottom();
AvaPlot.Refresh();
```

---

## 7. Implementation Order

Build in this sequence to enable early testing at each step:

| Step | Task | Validates |
|---|---|---|
| 1 | Create solution + projects, add packages, write .proto, verify codegen | Build pipeline |
| 2 | Implement `FileCacheService` + unit test read/write/TTL | Cache correctness |
| 3 | Implement `FinnhubClient`, test with hardcoded ticker via console | Finnhub connectivity + key |
| 4 | Implement `GeminiClient` (non-streaming first), test with small data set | Gemini connectivity + key |
| 5 | Implement `StocksAnalysisService` + wire up `Program.cs`, test with `grpcurl` | End-to-end server |
| 6 | Add streaming to `GeminiClient`, verify chunks arrive in gRPC stream | Streaming pipeline |
| 7 | Build Avalonia shell — window, layout, disclaimer | Client compiles + renders |
| 8 | Wire gRPC client into `MainWindowViewModel`, display raw text | Client ↔ server |
| 9 | Add ScottPlot candlestick chart | Full end-to-end UI |
| 10 | Enable `CACHE_AI_RESULTS`, verify cache hit skips Gemini call | Cache integration |

---

## 8. Testing the gRPC Service (Step 5 Validation)

Install `grpcurl` and test before building the client:

```bash
grpcurl -plaintext -d '{
  "ticker": "AAPL",
  "start_date": "2024-01-01",
  "end_date": "2024-03-31"
}' localhost:5001 stonks.StocksAnalysis/AnalyzeStock
```

---

## 9. `.gitignore` Additions

```
src/Stonks.Server/.env
**/Stonks/cache/
```

---

## 10. Key Technical Risks

| Risk | Mitigation |
|---|---|
| Finnhub free tier rate limits (60 calls/min) | File cache eliminates repeat calls for same date range |
| Gemini SSE parsing fragility | Test chunk boundary handling; buffer incomplete JSON across chunks |
| gRPC HTTP/2 without TLS on Windows | `Http2UnencryptedSupport` switch required on client; Kestrel must explicitly bind HTTP/2 |
| Avalonia DatePicker locale differences | Normalize to `DateOnly` / ISO 8601 strings before sending to server |
| Large OHLCV datasets in prompt | Cap prompt to ~500 bars; summarize older data if range is very wide |
