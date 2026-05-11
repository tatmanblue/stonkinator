using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Grpc.Net.Client;
using Stonks.Client.Desktop.ViewModels;
using Stonks.Shared.Grpc;

namespace Stonks.Client.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LoadClientConfig();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var host = Environment.GetEnvironmentVariable("SERVER_HOST") ?? "localhost";
            var port = Environment.GetEnvironmentVariable("SERVER_PORT") ?? "5001";

            var channel        = GrpcChannel.ForAddress($"http://{host}:{port}");
            var analysisClient = new StocksAnalysis.StocksAnalysisClient(channel);
            var historyClient  = new StocksHistory.StocksHistoryClient(channel);

            var searchAnalyze = new SearchAnalyzeViewModel(analysisClient);
            var dashboard     = new DashboardViewModel(historyClient, analysisClient);
            var mainVm        = new MainWindowViewModel(dashboard, searchAnalyze);

            desktop.MainWindow = new MainWindow { DataContext = mainVm };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void LoadClientConfig()
    {
        const string configFile = "client.env";
        if (!File.Exists(configFile)) return;

        foreach (var line in File.ReadAllLines(configFile))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || !trimmed.Contains('=')) continue;
            var idx = trimmed.IndexOf('=');
            var key = trimmed[..idx].Trim();
            var val = trimmed[(idx + 1)..].Trim();
            if (!string.IsNullOrEmpty(key))
                Environment.SetEnvironmentVariable(key, val);
        }
    }
}
