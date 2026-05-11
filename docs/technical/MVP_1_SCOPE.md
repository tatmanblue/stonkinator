# Stonkinator MVP Scope Analysis

> This document synthesizes the POC codebase, the priority spreadsheet, and general MVP thinking to recommend what should be in the MVP release.

---

## What the POC Already Gives You

Based on the repo, the POC has a working foundation:

- **Server**: ASP.NET Core backend that fetches historical market data (Polygon/Finnhub), sends chart images to an AI Vision/LLM (Gemini), and returns analysis results via gRPC. A file-based cache reduces redundant AI API calls during development.
- **Client**: Avalonia desktop UI with a single search/analysis screen where a user enters a ticker symbol, triggers analysis, and reads the AI-generated results.
- **Shared**: A gRPC contract (`stocks.proto`) connecting the two.

The core analysis loop — enter ticker → fetch data → AI analysis → display result — **works**. That is the foundation everything else builds on.

---

## The MVP's Job

An MVP for this project has one primary goal: **make the POC demonstrable and usable by someone other than the developer**. That means:

1. A person can sit down, run the app, and understand what it does without explanation.
2. They can analyze more than one stock in a session without having to remember what they looked at.
3. The experience doesn't feel like a developer tool — it feels like an application.

Everything in the MVP should serve that goal. Everything that doesn't should wait.

---

## Recommended MVP Scope

### Must Have (MVP)

#### Client — History / Dashboard

The single biggest usability gap in the POC is that there is no memory of what you've analyzed. You run the app, analyze a stock, and if you navigate away or restart, it's gone. The dashboard solves this, and it's the right anchor for the MVP client experience.

| Feature | Notes |
|---------|-------|
| Tabbed layout: Dashboard + Search/Analyze | The existing search screen becomes a tab rather than the whole app. Low structural risk since Avalonia supports tabs well. |
| Dashboard lists last 25 analyzed stocks | Symbol, analysis date, current price. Sorted by most recent. |
| Reanalyze a single stock from the dashboard | Right-click or button on each row. |
| Reanalyze all stocks in the list | A single "refresh all" action. |
| FIFO analysis queue | Prevents UI lockup when refreshing multiple stocks. The POC likely does analysis synchronously; this needs to become async/queued. |
| Double-click to open existing analysis | Opens the Search/Analyze tab pre-filled. Natural UX, low effort. |
| List management: replace oldest on overflow, update in place on re-analysis | These are the two edge cases that make the list feel correct rather than buggy. |
| Full analysis text readable | Already exists in the POC; just needs to survive the tab restructure. |

#### Client — Badges (Minimal Version)

Badges appear in the spreadsheet as both 1.0 and 1.1. For MVP, do the minimum: **define a small fixed set of badges** (oversold, overbought, and 2-3 others based on what the AI already returns) and display them on the dashboard list. The weighting/ordering system and chart overlays can wait for 1.1. Without *some* badges, the dashboard list is just a list of ticker symbols and dates — it has no analytical value at a glance.

| Feature | Notes |
|---------|-------|
| 3-5 defined badge types based on AI output | Pick the patterns the AI already identifies. Don't over-engineer the badge system. |
| Badges shown on dashboard list rows | Small visual indicators. Color-coded is enough. |

#### Server — Data Layer Refactor

The POC currently uses a file cache for AI responses. For MVP, you need actual persistence so the dashboard has data to show across sessions. This is the most significant server-side change required to support the client features above.

| Feature | Notes |
|---------|-------|
| Persistent storage for analysis history | Must store: ticker, analysis date, AI results, identified patterns/badges, price at time of analysis. |
| No EF Core; follow Cogitatio IDatabase pattern | As specified. |
| Namespaced table names | As specified — critical if sharing a DB instance. |
| SQLite support as the default for MVP | SQLite is the simplest path for a single-developer MVP — no server setup required, works cross-platform, and maps well to the Cogitatio pattern. Postgres and SQL Server can follow in a later release. |
| API endpoint(s) to serve analysis history to client | The dashboard needs to fetch history from the server on startup. The gRPC contract will need to be extended. |

#### Server — API for Re-analysis

The POC's flow is user-initiated from the client each time. The MVP needs a way for the client to say "re-analyze this ticker" and have the server pick it up. This is a modest extension of what already exists.

| Feature | Notes |
|---------|-------|
| Re-analysis endpoint (single ticker) | Reuses the existing analysis pipeline. Needs to update the stored record rather than insert a new one. |
| Queue-backed analysis so client doesn't block | Even a simple in-memory queue is fine for MVP. |

---

### Defer (Post-MVP)

These are all legitimately good ideas, but none of them are what makes the difference between "a POC" and "something demonstrable."

| Feature | Why Defer |
|---------|-----------|
| Configurable list size | Hardcode 25 for MVP. Configuration adds surface area without changing the experience. |
| Auto-refresh stock prices on startup | Nice-to-have, but requires a background process and adds complexity. Not needed to demonstrate the core value. |
| Badge weighting and ordering | The badge taxonomy needs real-world usage to inform weights. Defer until you've seen what badges actually surface. |
| Chart overlays for badges/indicators | This is the right long-term direction but is a significant UI effort in Avalonia. Schedule for 1.1. |
| Visual-first analysis results (no need to read text) | Same — the right direction, but a large design/UX effort. MVP is okay with text + badges. |
| Postgres / SQL Server support | Add after SQLite is working. The IDatabase abstraction makes this straightforward to add later. |
| JSON file-based database | Only useful for development/testing. Not needed in the shipped MVP. |
| Templated AI prompts | Good for maintainability, but you can refactor to this pattern after MVP. |
| Light/dark theming | 2.0. |
| Multi-AI-provider support (beyond Gemini) | Already partially designed for; add providers post-MVP. |

---

## Things Not in the Spreadsheet Worth Considering

A few gaps that may affect the MVP's "demonstrable" goal:

**Error handling and user feedback.** The POC likely surfaces errors as exceptions or nothing at all. For MVP, the client should tell the user when analysis fails (bad ticker, API error, server down) rather than silently doing nothing. This isn't on the spreadsheet but is important for any demo.

**Connection configuration.** Right now the client connects to a hardcoded server port. For MVP, at minimum the server port should be configurable in the client (even if just a settings file or simple UI field). Without this, running the app on a different machine is painful.

**Onboarding / empty state.** What does a new user see when they open the app and the dashboard is empty? A blank list is confusing. A simple "No stocks analyzed yet — go to Search to get started" placeholder is a small touch that makes the app feel finished.

---

## Summary

The MVP is essentially: **POC + persistence + dashboard + minimal badges + good error states**.

The tabbed layout, dashboard with history, and persistent storage are the three interlocking pieces that turn the POC into something you can hand to someone and have them use without coaching. The rest is refinement.

The server-side items in the spreadsheet (no EF Core, Cogitatio pattern, namespaced tables, SQLite) are all in service of that persistence layer — they're less "features" and more "how you build the one thing you need to build."

---

## See Also

- [MVP_1_IDEAS.md](MVP_1_IDEAS.md) — feature list with priorities
- [MVP_1_TECH.md](https://briefcase/STONKS/MVP_1_TECH.md) — concrete technical implementation plan (database schema, proto changes, server/client architecture, open questions)
