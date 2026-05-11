using Microsoft.AspNetCore.Server.Kestrel.Core;
using Stonks.Server.Ai;
using Stonks.Server.Badges;
using Stonks.Server.Cache;
using Stonks.Server.Data;
using Stonks.Server.MarketData;
using Stonks.Server.Repositories;
using Stonks.Server.Services;

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
builder.Services.AddSingleton<IDatabase, SqliteDatabase>();
builder.Services.AddSingleton<IAnalysisRepository, AnalysisRepository>();
builder.Services.AddSingleton<IBadgeExtractor, KeywordBadgeExtractor>();

builder.Services.AddSingleton<IMarketDataClient>(sp =>
{
    var http     = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var cache    = sp.GetRequiredService<ICacheService>();
    var provider = (Environment.GetEnvironmentVariable("STOCK_DATA_PROVIDER") ?? "massive").ToLowerInvariant();
    return provider switch
    {
        "finnhub" => (IMarketDataClient)new FinnhubClient(http, cache),
        "massive" => new PolygonClient(http, cache),
        "polygon" => new PolygonClient(http, cache),
        _ => throw new InvalidOperationException($"Unknown STOCK_DATA_PROVIDER: '{provider}'. Valid values: massive, polygon, finnhub.")
    };
});

builder.Services.AddSingleton<IAiClient>(sp =>
{
    var http      = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
    var cache     = sp.GetRequiredService<ICacheService>();
    var geminiLog = sp.GetRequiredService<ILogger<GeminiClient>>();
    var provider  = (Environment.GetEnvironmentVariable("AI_PROVIDER") ?? "gemini").ToLowerInvariant();
    return provider switch
    {
        "gemini" => (IAiClient)new GeminiClient(http, cache, geminiLog),
        _ => throw new InvalidOperationException($"Unknown AI_PROVIDER: '{provider}'. Valid values: gemini.")
    };
});

var app = builder.Build();

var db = app.Services.GetRequiredService<IDatabase>();
await db.EnsureSchemaAsync();

app.MapGrpcService<StocksAnalysisService>();
app.MapGrpcService<StocksHistoryService>();
app.Run();
