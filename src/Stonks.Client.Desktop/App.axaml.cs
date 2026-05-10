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
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var channel = GrpcChannel.ForAddress("http://localhost:5001");
            var grpcClient = new StocksAnalysis.StocksAnalysisClient(channel);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(grpcClient)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
