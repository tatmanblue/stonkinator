using Microsoft.AspNetCore.Server.Kestrel.Core;
using Stonks.Server.Ai;
using Stonks.Server.Cache;
using Stonks.Server.MarketData;
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
builder.Services.AddSingleton<IMarketDataClient>(sp =>
    new FinnhubClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<ICacheService>()));
builder.Services.AddSingleton<IAiClient>(sp =>
    new GeminiClient(sp.GetRequiredService<IHttpClientFactory>().CreateClient(), sp.GetRequiredService<ICacheService>()));

var app = builder.Build();
app.MapGrpcService<StocksAnalysisService>();
app.Run();
