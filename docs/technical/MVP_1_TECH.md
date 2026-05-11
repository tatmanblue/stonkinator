# Stonks MVP 1 — Technical Implementation Plan

> **Status:** Draft — awaiting answers to Open Questions before implementation begins.

---

## 1. What the POC Provides (Baseline)

| Component | Status |
|-----------|--------|
| gRPC streaming server (ASP.NET Core) | ✅ Working |
| Polygon + Finnhub market data clients | ✅ Working |
| Gemini AI client (streaming SSE) | ✅ Working |
| File-based AI response cache | ✅ Working |
| Avalonia desktop client — single search/analyze screen | ✅ Working |
| ScottPlot candlestick chart | ✅ Working |
| Analysis history / dashboard | ❌ Not built |
| Persistent database | ❌ Not built |
| Badge extraction | ❌ Not built |
| Tabbed layout | ❌ Not built |
| Error user feedback | ⚠️ Partial (text appended; no styled error states) |
| Connection configuration | ❌ Hardcoded port |

The core loop — ticker → market data → AI → chart + text — works. MVP layers persistence, history, and UX polish on top of it.

---

## 2. Architecture Changes

The POC architecture is unchanged. These additions compose on top of it:

```
Stonks.sln
src/
├── Stonks.Shared/
│   └── Protos/stocks.proto              ← EXTENDED: StocksHistory service + messages
│
├── Stonks.Server/
│   ├── Data/                            ← NEW: database abstraction
│   │   ├── IDatabase.cs
│   │   └── SqliteDatabase.cs
│   ├── Repositories/                    ← NEW: analysis history data access
│   │   ├── IAnalysisRepository.cs
│   │   └── AnalysisRepository.cs
│   ├── Badges/                          ← NEW: badge extraction from AI text
│   │   ├── IBadgeExtractor.cs
│   │   └── KeywordBadgeExtractor.cs
│   ├── Services/
│   │   ├── StocksAnalysisService.cs     ← MODIFIED: persist results + badges
│   │   └── StocksHistoryService.cs      ← NEW: history + re-analysis gRPC service
│   ├── Ai/ MarketData/ Cache/           ← UNCHANGED
│   └── Program.cs                       ← MODIFIED: wire new services + DB
│
└── Stonks.Client.Desktop/
    ├── ViewModels/
    │   ├── MainWindowViewModel.cs       ← MODIFIED: becomes tab host
    │   ├── DashboardViewModel.cs        ← NEW
    │   ├── AnalysisHistoryItemViewModel.cs ← NEW: per-row view model
    │   └── SearchAnalyzeViewModel.cs    ← NEW: extracted from MainWindowViewModel
    └── Views/
        ├── MainWindow.axaml             ← MODIFIED: TabControl host + disclaimer bar
        ├── DashboardView.axaml          ← NEW
        └── SearchAnalyzeView.axaml      ← NEW: extracted from MainWindow
```

---

## 3. New NuGet Packages

### Stonks.Server
| Package | Version | Purpose |
|---------|---------|---------|
| `Microsoft.Data.Sqlite` | latest stable | SQLite driver (no EF Core) |

### Stonks.Client.Desktop
No new packages required. Existing Avalonia, gRPC, and ScottPlot packages cover all MVP features.

---

## 4. Database Layer (`Stonks.Server/Data/`)

### 4.1 IDatabase Interface

Follows the Cogitatio `IDatabase` pattern — a thin ADO.NET wrapper, no ORM.

```csharp
public interface IDatabase
{
    Task<int> ExecuteAsync(string sql, IReadOnlyDictionary<string, object?>? parameters = null);
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, IReadOnlyDictionary<string, object?>? parameters, Func<IDataReader, T> mapper);
    Task<T?> QuerySingleAsync<T>(string sql, IReadOnlyDictionary<string, object?>? parameters, Func<IDataReader, T> mapper);
    Task EnsureSchemaAsync();
}
```

### 4.2 SqliteDatabase Implementation

- Connection string built from `DATABASE_PATH` env var (default: `%LocalAppData%/Stonks/stonks.db`).
- `EnsureSchemaAsync()` runs `CREATE TABLE IF NOT EXISTS` and `CREATE INDEX IF NOT EXISTS` on startup.
- Each public method opens and closes its own connection (SQLite connection pooling is handled by the driver).

### 4.3 Schema

All table names prefixed `stonks_` to avoid collisions when the database instance is shared.

```sql
CREATE TABLE IF NOT EXISTS stonks_analysis_history (
    id              INTEGER  PRIMARY KEY AUTOINCREMENT,
    ticker          TEXT     NOT NULL,
    analyzed_at     TEXT     NOT NULL,   -- ISO 8601 UTC
    start_date      TEXT     NOT NULL,   -- YYYY-MM-DD
    end_date        TEXT     NOT NULL,   -- YYYY-MM-DD
    ai_result_text  TEXT     NOT NULL,
    badges_json     TEXT     NOT NULL,   -- JSON array, e.g. ["Uptrend","Oversold"]
    price_at_close  REAL     NOT NULL    -- last close price in the analyzed range
);

CREATE UNIQUE INDEX IF NOT EXISTS stonks_idx_history_ticker
    ON stonks_analysis_history (ticker);   -- one row per ticker (upsert target)

CREATE INDEX IF NOT EXISTS stonks_idx_history_analyzed_at
    ON stonks_analysis_history (analyzed_at DESC);
```

### 4.4 AnalysisRepository

```csharp
public interface IAnalysisRepository
{
    // Insert or UPDATE existing row for this ticker (one row per ticker).
    // If ticker is new AND count is already at maxItems, deletes the oldest first.
    Task UpsertAsync(AnalysisRecord record, int maxItems = 25);

    // Returns up to `limit` records, most recent first.
    Task<IReadOnlyList<AnalysisRecord>> GetRecentAsync(int limit = 25);

    Task<AnalysisRecord?> GetByTickerAsync(string ticker);
}
```

**AnalysisRecord** (plain C# record, no ORM annotations):
```csharp
public record AnalysisRecord(
    string Ticker,
    DateTimeOffset AnalyzedAt,
    string StartDate,
    string EndDate,
    string AiResultText,
    IReadOnlyList<string> Badges,
    double PriceAtClose
);
```

**Upsert logic:**
1. If a row for `ticker` already exists → `UPDATE` it in place. Row position is preserved (satisfies "re-analysis updates in place, does not displace oldest").
2. If no row exists and `COUNT(*) >= maxItems` → `DELETE` the row with the oldest `analyzed_at`, then `INSERT`.
3. If no row exists and under the limit → `INSERT`.

---

## 5. Proto Extensions (`stocks.proto`)

A second service is added alongside the existing `StocksAnalysis`. The existing service and all its messages are **unchanged** (backward-compatible addition).

```proto
service StocksHistory {
  rpc GetAnalysisHistory (GetAnalysisHistoryRequest)  returns (GetAnalysisHistoryResponse);
  rpc ReanalyzeStock     (ReanalyzeStockRequest)      returns (ReanalyzeStockResponse);
  rpc ReanalyzeAll       (ReanalyzeAllRequest)         returns (ReanalyzeAllResponse);
}

message AnalysisHistoryItem {
  string          ticker         = 1;
  string          analyzed_at    = 2;   // ISO 8601 UTC
  string          start_date     = 3;
  string          end_date       = 4;
  string          ai_result_text = 5;
  repeated string badges         = 6;
  double          price_at_close = 7;
}

message GetAnalysisHistoryRequest  {}
message GetAnalysisHistoryResponse { repeated AnalysisHistoryItem items = 1; }

message ReanalyzeStockRequest  { string ticker = 1; }
message ReanalyzeStockResponse { bool   queued  = 1; }

message ReanalyzeAllRequest  {}
message ReanalyzeAllResponse { int32 queued_count = 1; }
```

**Re-analysis flow:** `ReanalyzeStock` and `ReanalyzeAll` acknowledge the request. The client then drives each analysis by calling the existing `StocksAnalysis.AnalyzeStock` RPC (which already streams results and persists them). No separate result-delivery mechanism is needed.

---

## 6. Server Changes

### 6.1 Persist Analysis Results

`StocksAnalysisService.AnalyzeStock` is extended after the AI stream completes:

1. Accumulate the full AI result text from streamed chunks (currently done for the AI cache — reuse that logic).
2. Extract badges via `IBadgeExtractor.Extract(fullText)`.
3. Capture `priceAtClose` = last bar's `close` value.
4. Call `IAnalysisRepository.UpsertAsync(...)`.

The response stream contract is unchanged.

### 6.2 Badge Extraction

**Proposed badge set (subject to Q1 confirmation):**

| Badge | Trigger keywords (case-insensitive) |
|-------|-------------------------------------|
| `Oversold` | "oversold", "rsi below 30", "extremely low rsi", "deeply oversold" |
| `Overbought` | "overbought", "rsi above 70", "extremely high rsi", "deeply overbought" |
| `Uptrend` | "uptrend", "bullish trend", "ascending", "higher highs", "higher lows" |
| `Downtrend` | "downtrend", "bearish trend", "descending", "lower lows", "lower highs" |
| `Consolidation` | "consolidation", "sideways", "range-bound", "neutral", "no clear trend" |

`KeywordBadgeExtractor.Extract(string analysisText)` → `IReadOnlyList<string>`. Each badge is emitted at most once regardless of how many keywords match.

### 6.3 Analysis Queue

For MVP, the queue is **client-side**: the `DashboardViewModel.ReanalyzeAllCommand` submits analyses sequentially (one at a time) in a background `Task`, updating the UI as each completes. No server-side queue infrastructure is needed for a single-user local deployment.

(A `Channel<T>`-backed background service would be added if the server goes multi-user in a future release.)

### 6.4 StocksHistoryService

```csharp
public class StocksHistoryService : StocksHistory.StocksHistoryBase
{
    public override async Task<GetAnalysisHistoryResponse> GetAnalysisHistory(
        GetAnalysisHistoryRequest request, ServerCallContext context)
    {
        var records = await repository.GetRecentAsync(25);
        // map records → proto AnalysisHistoryItem list
        return new GetAnalysisHistoryResponse { Items = { mapped } };
    }

    public override Task<ReanalyzeStockResponse> ReanalyzeStock(
        ReanalyzeStockRequest request, ServerCallContext context)
        => Task.FromResult(new ReanalyzeStockResponse { Queued = true });

    public override async Task<ReanalyzeAllResponse> ReanalyzeAll(
        ReanalyzeAllRequest request, ServerCallContext context)
    {
        var records = await repository.GetRecentAsync(25);
        return new ReanalyzeAllResponse { QueuedCount = records.Count };
    }
}
```

### 6.5 Program.cs Additions

```csharp
builder.Services.AddSingleton<IDatabase, SqliteDatabase>();
builder.Services.AddSingleton<IAnalysisRepository, AnalysisRepository>();
builder.Services.AddSingleton<IBadgeExtractor, KeywordBadgeExtractor>();
// ...
app.MapGrpcService<StocksHistoryService>();   // alongside existing MapGrpcService<StocksAnalysisService>()
```

`.env.example` addition:
```
DATABASE_PATH=   # optional; defaults to %LocalAppData%/Stonks/stonks.db
```

---

## 7. Client Changes

### 7.1 Tab Structure

`MainWindow.axaml` becomes a `TabControl` host. The disclaimer bar is pinned **outside** the tab control so it's always visible.

```
┌──────────────────────────────────────────────────────────────────┐
│  [Dashboard]  [Search / Analyze]                                 │  ← TabControl header
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  <tab content — DashboardView or SearchAnalyzeView>              │
│                                                                  │
├──────────────────────────────────────────────────────────────────┤
│  ⚠ For educational purposes only. Not financial advice.          │  ← always visible
└──────────────────────────────────────────────────────────────────┘
```

The existing `MainWindowViewModel` is refactored:
- `MainWindowViewModel` → tab host: owns `DashboardViewModel` + `SearchAnalyzeViewModel`, exposes `SelectedTabIndex`
- `SearchAnalyzeViewModel` → existing analysis logic moved here (minimal changes)
- `DashboardViewModel` → new

### 7.2 DashboardViewModel

```csharp
public class DashboardViewModel : INotifyPropertyChanged
{
    public ObservableCollection<AnalysisHistoryItemViewModel> Items { get; }
    public bool IsEmpty => Items.Count == 0;
    public bool IsRefreshing { get; private set; }
    public string? ErrorMessage { get; private set; }

    public ICommand ReanalyzeAllCommand { get; }

    // Called on startup; re-called after each analysis completes
    public async Task LoadHistoryAsync();
}
```

`AnalysisHistoryItemViewModel` — per-row:
```csharp
public class AnalysisHistoryItemViewModel
{
    public string Ticker { get; }
    public string AnalyzedAt { get; }          // formatted for display
    public string PriceAtClose { get; }        // "$189.42"
    public IReadOnlyList<string> Badges { get; }
    public string AiResultText { get; }
    public ICommand ReanalyzeCommand { get; }
}
```

### 7.3 Dashboard View Layout

```
┌────────────────────────────────────────────────────────────────┐
│  [Re-analyze All]                            [↻ refreshing...] │  ← toolbar
├───────┬──────────────┬────────┬──────────────────┬─────────────┤
│ AAPL  │ 2026-05-10   │ $189   │ [Uptrend]        │ [↺]         │
│ TSLA  │ 2026-05-09   │ $177   │ [Overbought]     │ [↺]         │
│ MSFT  │ 2026-05-08   │ $421   │ [Uptrend][Overbought] │ [↺]    │
│ ...                                                             │
└────────────────────────────────────────────────────────────────┘

[empty state shown when no items:
 "No stocks analyzed yet — go to Search / Analyze to get started."]
```

- `[↺]` per-row button triggers re-analysis for that single ticker.
- Double-clicking a row populates `SearchAnalyzeViewModel` (ticker, start/end dates, existing AI text) and switches to the Search / Analyze tab.
- Badges are displayed as color-coded text chips (e.g., green for Uptrend, amber for Overbought/Oversold, gray for Consolidation). Full icon badges deferred to 1.1.

### 7.4 Connection Configuration

Add `client.env` alongside the client binary (loaded via `DotNetEnv.Env.Load("client.env")` at startup; silently ignored if absent):

```
SERVER_HOST=localhost
SERVER_PORT=5001
```

The client falls back to `localhost:5001` if the file is missing. Document this in `docs/INSTALLATION.md`.

### 7.5 Error Handling

Replace the current raw `[Error]` text append with structured error states:

- `SearchAnalyzeViewModel.ErrorMessage` property bound to a styled error banner (red/amber `Border`) visible above the chart area.
- `DashboardViewModel.ErrorMessage` bound to a similar banner on the dashboard tab.
- Specific user-readable messages for known error conditions:
  - Invalid ticker → "Ticker symbol not recognized."
  - Server unreachable → "Could not connect to server. Check that the server is running."
  - Market data error → "Market data unavailable for this ticker or date range."
  - AI error → "AI analysis failed. Please try again."

---

## 8. Implementation Order

| Step | Task | Validates |
|------|------|-----------|
| 1 | Add `Microsoft.Data.Sqlite`; implement `IDatabase` + `SqliteDatabase`; call `EnsureSchemaAsync` from server startup | DB file created, schema tables exist |
| 2 | Implement `IAnalysisRepository` + `AnalysisRepository`; test upsert/query/delete-oldest logic | Repository correctness; edge cases (at-capacity, re-analyze existing) |
| 3 | Implement `IBadgeExtractor` + `KeywordBadgeExtractor`; unit test against sample AI output | Badge extraction accuracy |
| 4 | Modify `StocksAnalysisService` to persist results + badges after stream completes | End-to-end persistence: run an analysis, check the DB row |
| 5 | Extend `stocks.proto` with `StocksHistory` service + messages; implement `StocksHistoryService`; register in `Program.cs` | New gRPC endpoints callable via grpcurl |
| 6 | Extract `SearchAnalyzeViewModel` from `MainWindowViewModel`; restructure `MainWindow` as `TabControl` host | Client compiles; search/analyze tab works as before |
| 7 | Implement `DashboardViewModel` + `DashboardView.axaml`; wire `GetAnalysisHistory` on startup | Dashboard renders stored history |
| 8 | Implement double-click row navigation (dashboard → search/analyze, pre-filled) | Navigation UX |
| 9 | Implement per-row re-analyze; implement re-analyze all (sequential background task, progress visible) | Full re-analysis flow |
| 10 | Add empty-state placeholder to dashboard | First-run UX |
| 11 | Add `client.env` config; wire `SERVER_HOST` / `SERVER_PORT` in client startup | Connection configurable without rebuild |
| 12 | Implement structured error banners in both tabs; replace raw text appends | Error UX |

---

## 9. Open Questions & Decisions Needed

The following must be resolved before implementation begins. Items marked **BLOCKING** affect schema or API contracts that are expensive to change later.

---

### Q1 — Badge Set  *(BLOCKING)*

The badge set is marked "TBD" in `MVP_1_IDEAS.md`. The plan proposes five:

| Badge | Description |
|-------|-------------|
| `Oversold` | RSI / momentum indicators suggest the stock is oversold |
| `Overbought` | RSI / momentum indicators suggest the stock is overbought |
| `Uptrend` | Price action showing higher highs / higher lows, bullish bias |
| `Downtrend` | Price action showing lower lows / lower highs, bearish bias |
| `Consolidation` | Price moving sideways; no clear directional trend |

**Decision needed:** Is this the right set? Any additions, removals, or renames?

Also: should badge extraction use **keyword matching on AI text** (simpler, no extra API cost) or a **separate structured AI call** (more accurate, doubles latency + cost per analysis)?  
Recommendation: keyword matching for MVP.  
**Decision:** Confirmed the proposed badge set. Extraction will be keyword-based for MVP, with the understanding that it may be noisy and imperfect. The AI text is always available for users to read the full analysis context.

---

### Q2 — "Current Price" — Live or Stored?

`MVP_1_SCOPE.md` says the dashboard shows "current stock price." Two options:

| Option | What it shows | Complexity |
|--------|---------------|------------|
| **A — Stored (recommended)** | Closing price of the last bar at analysis time; labeled "Price at [date]" | None — already captured |
| **B — Live** | Fetched from the market data API on dashboard load | One extra API call per ticker per session; adds latency, possible rate-limit pressure |

**Decision needed:** Option A or B?  
Recommendation: Option A for MVP, labeled clearly as "Price at analysis." Live price can be a 1.1 feature.  
**Decision:** Confirmed stored price for MVP, labeled "Price at analysis." The date is visible in the AI text for reference.

---

### Q3 — Re-analysis Date Range

When a user re-analyzes a stock, what date range should be used?

| Option | Behavior |
|--------|----------|
| **A — Original range** | Reuses the start/end dates from the stored record |
| **B — Trailing window (recommended)** | Always uses a fresh trailing 3-month window from today |

**Decision needed:** Option A or B?  
Recommendation: Option B — the purpose of re-analysis is to get fresh data.  
**Decision:** Confirmed trailing window for re-analysis. The original date range is visible in the AI text if the user wants to reference it.

---

### Q4 — History Model: One Row Per Ticker vs. Full Audit Log  *(BLOCKING)*

`MVP_1_SCOPE.md` states re-analysis "updates its entry in place," implying one row per ticker. The schema above reflects this. A full audit log (multiple rows per ticker, timestamped) would be richer but changes the schema and dashboard UX.

**Decision needed:** Confirm one row per ticker (replace on re-analyze) for MVP?  
**Decision:** Confirmed one row per ticker for MVP. A full audit log can be considered for a future release once the core features are solid.

---

### Q5 — Cogitatio IDatabase Pattern

The `MVP_1_IDEAS.md` links to the Cogitatio project's data access pattern. Should we:

| Option | Approach |
|--------|----------|
| **A — Copy Cogitatio code** | Pull `IDatabase` + implementation from the Cogitatio repo and adapt |
| **B — Implement from scratch (recommended)** | Write our own `IDatabase` + `SqliteDatabase` using the pattern as a guide |

**Decision needed:** Option A or B?  
Recommendation: Option B — avoids a copy-paste dependency and lets us tailor to async SQLite patterns.  
**Decision:** Accepted Option B — implement our own `IDatabase` and `SqliteDatabase` following the Cogitatio pattern as a reference.

---

### Q6 — gRPC vs REST for New Endpoints

The server currently runs HTTP/2 only (Kestrel). Adding REST endpoints would require configuring a mixed-protocol listener (a second port, or `HttpProtocols.Http1AndHttp2` on the same port with TLS). To keep things simple, the plan proposes all new endpoints as gRPC unary calls (proto extension).

**Decision needed:** Is gRPC-only acceptable for history and re-analysis, or is a REST API required for any of these?  
**Decision:** gRPC-only is fine for MVP. 

---

### Q7 — CLAUDE.md Path Correction

`CLAUDE.md` references `docs/DESIGN.md`, but the file lives at `docs/technical/DESIGN.md`. This causes a broken link.

**Decision needed:** Should this be corrected as part of MVP work?  
**Decision:** I already made the change
---

## 10. Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Badge keyword matching produces false positives/negatives | Medium | Tune keyword lists against real AI output from a few test runs; accept imperfection for MVP |
| SQLite schema needs migration post-MVP | Low | Add a `stonks_schema_version` table; apply incremental migrations in `EnsureSchemaAsync` from the start |
| Avalonia `DataGrid` behavior differences across platforms | Low-Medium | Test on Windows first (primary platform); use `ItemsControl` + item template as a simpler fallback if DataGrid causes issues |
| gRPC proto addition breaks existing client | None | Adding a new service to the proto is fully backward-compatible; existing `StocksAnalysis` clients are unaffected |
| Re-analyze-all blocks UI if 25 × API latency stacks up | Medium | Show per-item progress in the dashboard; provide a cancel/stop button |
| File-based AI cache conflicts with persistent history (stale badge data) | Low | Cache stores raw AI text only; badges are re-extracted fresh on every analysis, so cache hits do not cause stale badges |
