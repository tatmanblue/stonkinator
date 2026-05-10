using Avalonia;
using System;

namespace Stonks.Client.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Required for gRPC HTTP/2 without TLS on .NET
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
