# Stonks MVP Feature Ideas

> **Priority Key**
> - `1.0` — Must have for MVP
> - `1.1` — Strong candidate; include if feasible
> - `2.0` — Defer to a future release
> - *(no priority)* — Under consideration / not yet prioritized

---

## Client Application

*Features identified primarily in the desktop client. Server-side changes may also be required to support these.*

### Dashboard (MVP)

| Priority | Feature |
|----------|---------|
| 1.0 | App opens directly to the dashboard |
| 1.0 | Dashboard is one tab on the client view |
| 1.0 | Search/Analyze screen (as it exists now) is a second tab |
| 2.0 | Configuration options is a third tab |
| 1.0 | Dashboard lists the last 25 stocks searched |
| 2.0 | Number of last searched/analyzed stocks is configurable |
| 1.0 | Each list entry shows: stock symbol, date of analysis, identified pattern/trend badges, and current stock price |
| 1.0 | List is sorted by most recent |
| 1.0 | Empty-state placeholder shown when no stocks have been analyzed yet |

### Analysis & Re-analysis (MVP)

| Priority | Feature |
|----------|---------|
| 1.0 | User can request re-analysis of an individual stock in the list |
| 1.0 | User can request re-analysis of all stocks in the list |
| 1.0 | Each analysis request is queued and processed FIFO |
| 1.0 | Analyzing a new stock replaces the oldest entry when the max list size is exceeded |
| 1.0 | Re-analyzing a stock already in the list updates its entry in place (does not displace the oldest entry) |
| 1.0 | Double-clicking a list item opens the Search/Analysis screen pre-populated with that stock's existing analysis |
| 1.0 | Users can read the full analysis text |
| 2.0 | On startup, stock prices automatically refresh to current values (if configured) |

### Badges & Indicators

| Priority | Feature |
|----------|---------|
| 1.0 | A badge represents a specific analysis finding (e.g., oversold) — exact set of badges TBD |
| 1.1 | Badges displayed on the dashboard list are derived from analysis findings |
| 1.1 | Analysis results show badges and/or indicators overlaid on the chart |
| 2.0 | Badges are weighted and ordered by weight |
| 2.0 | Badge weighting criteria TBD |

### Visual Design

| Priority | Feature |
|----------|---------|
| 2.0 | Analysis results should be primarily visual — users should not need to read analysis text to understand results |
| 2.0 | Client should support theming, at minimum light and dark mode |

### Reliability & Polish

| Priority | Feature |
|----------|---------|
| 1.0 | Client shows a clear error message when analysis fails (bad ticker, API error, server unreachable) |
| 1.0 | Server port (and host) is configurable on the client without a rebuild |
| 1.0 | Dashboard shows an empty-state placeholder when no stocks have been analyzed |

---

## Server Application

*Features identified primarily in the server. Client-side changes may also be required to support these.*

### Architecture & Data Access (MVP)

| Priority | Feature |
|----------|---------|
| 1.0 | Do not use EF Core |
| 1.0 | Follow the data access pattern established in the [Cogitatio project](https://github.com/tatmanblue/Cogitatio/blob/main/src/Cogitatio) (`IDatabase` interface + `SqlServer` implementation) |
| 1.0 | Table names must be unique/namespaced for Stonks — the database instance may be shared with other applications (hosting cost tradeoff; acknowledged design compromise) |

### Configuration

| Priority | Feature |
|----------|---------|
| 2.0 | AI prompt text is templated to allow prompt editing without requiring a rebuild |

### Database Support

| Priority | Feature |
|----------|---------|
| 2.0 | Postgres |
| 2.0 | MS SQL Server |
| 1.0 | SQLite (default for MVP — no server setup required, cross-platform) |
| 2.0 | JSON (file-based) |
