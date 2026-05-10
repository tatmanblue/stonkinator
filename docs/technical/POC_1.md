# Stonks POC — Feature Scope

## Project Summary

Stonks is a local, single-user desktop application where:
- A **C# ASP.NET Core server** fetches market data, orchestrates AI calls, and serves results via gRPC/REST
- An **Avalonia desktop client** provides the UI
- The app delegates all technical analysis to AI APIs (LLM + Vision) — it does not implement TA logic itself

---

## Recommended POC Features

The goal of the POC is to prove the **end-to-end flow**: user inputs a ticker → server fetches data → AI analyzes it → client displays results.

### Server (Stonks.Server)

| Feature | Notes |
|---|---|
| Single market data provider integration | Pick one (e.g. Finnhub — has a free tier). Fetch historical OHLCV for a ticker + date range. |
| Single AI provider integration | Pick one (e.g. Claude or Grok). Send price data as structured text to LLM, return analysis. |
| One gRPC endpoint | `AnalyzeStock(ticker, dateRange)` → streaming response so client sees results as they arrive |
| `.env` config via DotNetEnv | API keys for market data + AI provider |
| Shared models project | Protobuf contracts + any shared C# types |
| File-based response cache | Cache OHLCV market data and optionally AI analysis results to disk to reduce repeated API calls. See Caching Strategy below. |

### Desktop Client (Stonks.Client.Desktop)

| Feature | Notes |
|---|---|
| Ticker input + date range picker | Simple form |
| "Analyze" button → gRPC call to server | Shows loading state while streaming |
| Display AI analysis text | Streaming text display as chunks arrive via gRPC |
| Basic OHLC/candlestick chart | Even a simple chart using a library like LiveChartsCore or ScottPlot.Avalonia |
| Disclaimer banner | Required per design; non-intrusive footer |

---

## Caching Strategy

### What to Cache

| Data | Cache? | Rationale |
|---|---|---|
| OHLCV market data (Finnhub) | **Yes** | Historical OHLCV is immutable — the same ticker + date range always returns the same data. Eliminates redundant API calls and protects free-tier rate limits. |
| AI analysis results (Gemini) | **Optional** | Avoids repeated Gemini calls for the same input, but should be easy to bypass/clear while tuning prompts. Controlled via `.env` flag. |

### Cache Key Design

Both caches use the key structure: `{TICKER}_{startDate}_{endDate}` (e.g. `AAPL_20240101_20240630`).

For AI results, append a short hash of the prompt template so that prompt changes automatically invalidate cached responses.

### Storage Location

Cache files are stored as JSON under a platform-appropriate local directory:

| Platform | Path |
|---|---|
| Windows | `%LOCALAPPDATA%\Stonks\cache\` |
| Linux / macOS | `~/.local/share/stonks/cache/` |

Use two subdirectories: `ohlcv\` and `analysis\`.

### TTL Rules

| Cache | TTL |
|---|---|
| OHLCV — date range fully in the past | Indefinite — historical data never changes |
| OHLCV — date range includes today | 1 hour — intraday data can still update |
| AI analysis | Indefinite — tied to the immutability of its OHLCV source; clear manually when changing prompts |

### Implementation Notes

- No additional NuGet packages required — `System.IO` + `System.Text.Json` is sufficient.
- Implement as a simple `ICacheService` interface with `TryGet<T>` / `Set<T>` / `Invalidate` methods so it can be swapped post-POC (e.g. SQLite or in-memory).
- Cache is server-side only; the client is unaware of it.
- A `CACHE_AI_RESULTS=true` flag in `.env` controls whether Gemini responses are persisted to disk.

---

## What to Defer (Post-POC)

- Vision model / chart-image analysis
- Multiple AI or data providers
- Watchlist / saved analyses
- Technical indicator overlays (RSI, MACD, etc.)
- Pattern recognition display
- Web/mobile clients

---

## Key Decision Points Before Starting

1. **Market data provider** — Finnhub has a free tier; Polygon has a better free tier for historical data. Which do you have access to?
2. **AI provider** — The design mentions Grok, Claude, GPT. Which API key(s) do you have ready?
3. **gRPC vs REST for POC** — gRPC is correct long-term but adds setup friction. Would you accept plain REST for the POC and migrate to gRPC after?


**Decision 1:** Finnhub for market data (free tier sufficient for historical OHLCV).   
**Decision 2:** Gemini since it has a free account.   
**Decision 3:** Go ahead with gRPC for the POC to prove the intended architecture, even if it adds some initial setup time.

__Briefcase:__ Please see the file POC_1_CRED.md in the "stonks" project for the API keys and credentials needed to run the POC.