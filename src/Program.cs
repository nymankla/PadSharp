using Avalonia;

namespace PadSharp;

internal static class Program
{
    /// <summary>
    /// Starts the editor. Arguments are files to open; <c>--screenshot file.png</c> draws about
    /// 120 frames without taking focus, saves a picture of the window and exits.
    /// </summary>
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Used by the app and by design tools.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
