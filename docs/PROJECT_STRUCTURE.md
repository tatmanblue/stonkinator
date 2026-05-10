# Project Structure

```
stonks/
├── src/
│   ├── Stonks.Server/           # ASP.NET Core backend
│   │   ├── Ai/                  # AI provider clients (Gemini)
│   │   ├── Cache/               # File-based AI response cache
│   │   ├── MarketData/          # Market data provider clients (Polygon, Finnhub)
│   │   ├── Services/            # Core analysis orchestration
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
│   └── Stonks.Shared/           # Shared contracts used by server and clients
│       └── Protos/
│           └── stocks.proto     # gRPC service & message definitions
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

- Fetches historical market data from external providers (Polygon, Finnhub)
- Sends chart images and data to AI Vision/LLM APIs for technical analysis
- Caches AI responses to disk to reduce redundant API calls
- Exposes results to clients via gRPC and REST

Key files:
- `Ai/GeminiClient.cs` — Gemini AI integration
- `MarketData/PolygonClient.cs`, `FinnhubClient.cs` — market data providers
- `Cache/FileCacheService.cs` — disk-based response cache
- `Services/StocksAnalysisService.cs` — orchestrates the analysis pipeline

### `Stonks.Client.Desktop`

The cross-platform Avalonia desktop client (Windows, macOS, Linux). Responsibilities:

- Provides the user interface for entering ticker symbols and date ranges
- Displays technical analysis results returned by the server
- Handles user settings and local preferences

Key files:
- `ViewModels/MainWindowViewModel.cs` — primary application view model
- `ViewModels/AsyncCommand.cs` — async command helper for UI actions

### `Stonks.Shared`

A shared class library consumed by both the server and any future clients. Currently contains:

- `Protos/stocks.proto` — gRPC service contract and message definitions
