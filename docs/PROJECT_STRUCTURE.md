# Project Structure

```
stonks/
├── src/
│   ├── Stonks.Server/           # ASP.NET Core backend
│   │   ├── Ai/                  # AI provider clients (Gemini)
│   │   ├── Cache/                     # File-based AI response cache
│   │   ├── Data/                      # SQLite database abstraction
│   │   ├── MarketData/                # Market data & options data provider clients (Massive/Polygon, Finnhub)
│   │   ├── Repositories/               # Persistence for saved options evaluations
│   │   ├── Services/                   # Core analysis & options evaluation orchestration
│   │   ├── Properties/
│   │   ├── .env                 # Local environment config (not committed)
│   │   ├── .env.example         # Template for .env
│   │   ├── appsettings.json
│   │   └── Program.cs           # Server entry point & DI setup
│   │
│   ├── Stonks.Client.Desktop/   # Avalonia cross-platform desktop client
│   │   ├── ViewModels/          # MVVM view models & commands
│   │   ├── Views/               # Avalonia XAML views
│   │   ├── MainWindow.axaml     # Main application window
│   │   └── Program.cs           # Client entry point
│   │
│   ├── Stonks.Shared/            # Shared contracts used by server and clients
│   │   ├── Calculations/               # Shared calculation logic (straddle/strangle)
│   │   └── Protos/
│   │       └── stocks.proto     # gRPC service & message definitions
│   │
│   └── Stonks.Shared.Tests/      # xUnit tests for Stonks.Shared
│
├── docs/
│   ├── technical/               # Design docs, POC write-ups, AI assessments
│   ├── INSTALLATION.md          # Setup and run instructions
│   └── PROJECT_STRUCTURE.md     # This file
│
├── Stonks.slnx                  # Solution file
├── CLAUDE.md                    # AI assistant instructions
├── LICENSE
└── README.md
```

## Projects

### `Stonks.Server`

The backend service. Responsibilities:

- Fetches historical market data from external providers (Massive/Polygon, Finnhub)
- Fetches options data (expiration dates, chain premiums) where the provider supports it
- Sends chart images and data to AI Vision/LLM APIs for technical analysis
- Caches AI responses to disk to reduce redundant API calls
- Persists analysis history and saved options evaluations to SQLite
- Exposes results to clients via gRPC and REST

Key files:
- `Ai/GeminiClient.cs` — Gemini AI integration
- `MarketData/MassiveClient.cs`, `FinnhubClient.cs` — market data providers
- `MarketData/IOptionsDataClient.cs`, `MassiveOptionsClient.cs`, `UnsupportedOptionsDataClient.cs` — options data abstraction; only Massive/Polygon implements it, Finnhub falls back to the unsupported stub
- `Cache/FileCacheService.cs` — disk-based response cache
- `Data/SqliteDatabase.cs` — SQLite schema & connection management
- `Repositories/OptionsEvaluationRepository.cs` — persists saved options evaluations (upsert by ticker + expiration)
- `Services/StocksAnalysisService.cs` — orchestrates the analysis pipeline
- `Services/OptionsMarketDataService.cs`, `OptionsEvaluationsService.cs` — gRPC services backing the Options Evaluator tab

### `Stonks.Client.Desktop`

The cross-platform Avalonia desktop client (Windows, macOS, Linux). Responsibilities:

- Provides the user interface for entering ticker symbols and date ranges
- Displays technical analysis results returned by the server
- Provides the Options Evaluator: straddle/strangle break-even calculator with auto-fetched or manually-entered premiums
- Handles user settings and local preferences

Key files:
- `ViewModels/MainWindowViewModel.cs` — primary application view model; hosts the Dashboard, Search/Analyze, and Options Evaluator tabs
- `ViewModels/AsyncCommand.cs` — async command helper for UI actions
- `ViewModels/OptionsEvaluatorViewModel.cs` — Options Evaluator tab logic (fetch expirations/chain, manual fallback, save/load evaluations)
- `ViewModels/StrikeRowViewModel.cs` — per-strike row calculations (cost, break-even, % change)
- `ViewModels/SavedEvaluationSummaryViewModel.cs` — saved-evaluations list entries
- `Views/OptionsEvaluatorView.axaml` — Options Evaluator tab layout

### `Stonks.Shared`

A shared class library consumed by both the server and any future clients. Currently contains:

- `Protos/stocks.proto` — gRPC service contract and message definitions
- `Calculations/StraddleCalculator.cs` — straddle/strangle cost, break-even, and % change formulas, covered by `Stonks.Shared.Tests`
