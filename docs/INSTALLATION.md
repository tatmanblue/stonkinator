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
| `DATABASE_PATH` | Override SQLite database file location (optional) | *(defaults to `%LocalAppData%/Stonks/stonks.db`)* |

### 3. Run the server

```bash
dotnet run --project src/Stonks.Server
```

The server will start on the configured `SERVER_PORT` (default `5001`). On first run it creates the SQLite database automatically.

### 4. (Optional) Configure client connection

If the server is running on a non-default port or host, create `client.env` next to the client executable:

```bash
cp src/Stonks.Client.Desktop/client.env.example src/Stonks.Client.Desktop/client.env
```

Edit `client.env`:

| Variable | Description | Default |
|---|---|---|
| `SERVER_HOST` | Hostname of the Stonks server | `localhost` |
| `SERVER_PORT` | Port of the Stonks server | `5001` |

If `client.env` is absent the client uses `localhost:5001` automatically.

### 5. Run the desktop client

In a separate terminal:

```bash
dotnet run --project src/Stonks.Client.Desktop
```

The client opens to the Dashboard tab. If no stocks have been analyzed yet, switch to the **Search / Analyze** tab to run your first analysis. Both the server and client must be running at the same time for the application to function.

## Notes

- The `.env` file is excluded from source control. Never commit your API keys.
- `client.env` is also excluded from source control.
- Set `CACHE_AI_RESULTS=true` during development to avoid redundant AI API calls while iterating on UI or server logic.
- Analysis history is stored in SQLite at `%LocalAppData%/Stonks/stonks.db` (Windows). The file is created automatically on first run.
