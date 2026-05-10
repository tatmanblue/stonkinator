# Installation & Running (Development)

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- API key for a supported **market data provider** (Polygon or Finnhub)
- API key for a supported **AI provider** (Gemini)

## Steps

### 1. Clone the repository

```bash
git clone https://github.com/your-org/stonks.git
cd stonks
```

### 2. Configure environment variables

The server reads configuration from a `.env` file. Copy the example and fill in your keys:

```bash
cp src/Stonks.Server/.env.example src/Stonks.Server/.env
```

Edit `src/Stonks.Server/.env`:

| Variable | Description | Example |
|---|---|---|
| `STOCK_DATA_API_KEY` | API key for your market data provider | `abc123` |
| `STOCK_DATA_PROVIDER` | Market data provider name | `polygon` or `finnhub` |
| `AI_API_KEY` | API key for your AI provider | `xyz789` |
| `AI_PROVIDER` | AI provider name | `gemini` |
| `AI_MODEL` | Model identifier to use | `gemini-flash-latest` |
| `CACHE_AI_RESULTS` | Cache AI responses to disk to reduce API calls | `true` or `false` |
| `SERVER_PORT` | Port the server listens on | `5001` |

### 3. Run the server

```bash
dotnet run --project src/Stonks.Server
```

The server will start on the configured `SERVER_PORT` (default `5001`).

### 4. Run the desktop client

In a separate terminal:

```bash
dotnet run --project src/Stonks.Client.Desktop
```

The client connects to the server automatically on the default port. Both processes must be running at the same time for the application to function.

## Notes

- The `.env` file is excluded from source control. Never commit your API keys.
- Set `CACHE_AI_RESULTS=true` during development to avoid redundant AI API calls while iterating on UI or server logic.
