using Avalonia;
using System;
using System.Threading;
using Stonks.Client.Desktop;

// Set STA thread for Avalonia on Windows
if (OperatingSystem.IsWindows())
{
    Thread.CurrentThread.ApartmentState = ApartmentState.STA;
}

// Required for gRPC HTTP/2 without TLS on .NET
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
