# Stonks

**AI-Powered Technical Analysis for Stocks & Financial Instruments**

> **Educational Tool Only** — Not Financial Advice

Stonks is an application that leverages modern AI models to provide technical analysis, pattern recognition, and actionable insights for stocks and other financial instruments. It is designed primarily for speculative day traders and retail investors who want to deepen their understanding of technical analysis.

**Important Disclaimer**: This application is built for **educational and research purposes only**. It does not provide financial advice, nor does it execute or recommend live trades. Always do your own research and consult qualified professionals before making investment decisions.

---

## Project Overview

Stonks follows a **client-server architecture**:

- **Stonks Server** — The brain of the application. Handles data retrieval from market APIs, orchestration with AI models (LLMs + Vision), technical analysis logic, and serves results via APIs.
- **Stonks Desktop Client** — A modern cross-platform desktop application that provides an intuitive user interface for traders to interact with the system.

Future clients (Web, Mobile) are possible and the architecture has been designed with this extensibility in mind.

---

## Key Features

- AI-driven technical analysis (patterns, trends, indicators)
- Support for classic patterns (Head & Shoulders, Cup & Handle, etc.)
- Key indicators: RSI, MACD, Moving Averages, Bollinger Bands, Fibonacci, Volume Analysis, Support/Resistance
- Interactive candlestick charts with overlays
- (optional) Real-time and historical market data integration
- Watchlist and saved analysis management
- Clear, educational explanations of AI insights
- Cross-platform desktop support (Windows, macOS, Linux)

---

## Architecture

```
┌─────────────────────┐       ┌──────────────────────┐
│  Stonks Server      │       │   Stonks Desktop     │
│   (ASP.NET Core)    │◄─────►│   Client (Avalonia)  │
└─────────────────────┘       └──────────────────────┘
│
┌──────────────────────┼──────────────────────┐
▼                      ▼                      ▼
Market Data APIs         AI / LLM APIs           Database
(Polygon, Finnhub, etc.)   (Grok, Claude, GPT, etc.)
```

### Component Roles

- **Server (`Stonks.Server`)**: 
  - Market data fetching & caching
  - AI prompt orchestration and response processing
  - REST + gRPC APIs
  - Authentication & security
  - Data persistence

- **Desktop Client (`Stonks.Client.Desktop`)**: 
  - Rich interactive UI
  - Chart visualization
  - User experience & local settings
  - Consumes server APIs

- **Shared Libraries**: Common models, contracts, and utilities used across server and clients.

---

## Technical Stack

* **Language:** C# 10 / .NET
* **Frameworks:** ASP.NET Core (Server), Avalonia UI (Desktop)
* **Communication:** gRPC (High-performance data streaming) & REST (Standard API operations)
* **AI Integration:** Support for Vision-Language Models (VLM) and Large Language Models (LLM)


---

## Getting Started

### Prerequisites

- .NET 10.0 SDK or later
- API keys for chosen market data and AI providers

### Installation & Running (Development)

TBD

## Project Structure

TBD

---

## Roadmap

- [ ] MVP with core analysis flow
- [ ] Multi-provider AI and data source support
- [ ] Advanced charting and pattern visualization

---

## Disclaimer

**This software is for educational purposes only.**
It does not constitute financial, investment, or trading advice.  
The developers are not registered financial advisors.  Use at your own risk. The author(s) and contributors 
shall not be held liable for any financial losses or damages resulting from the use of this software.  
Always do your own research and consult a licensed financial advisor before making any investment decisions.   

AI-generated analysis is experimental and may contain inaccuracies. Users should verify all data 
against official financial sources before making any decisions.

**Past performance does not guarantee future results.**  
Trading and investing involves substantial risk of loss. You can lose all your money.


---

## License

[License](LICENSE)

---

## Contributing

Contributions are welcome! Please see `CONTRIBUTING.md` for guidelines.

---

## Status/Version
Proof of Concept/Limited updates
2026.05.11

