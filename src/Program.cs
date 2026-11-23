using Avalonia;

namespace PadSharp;

internal static class Program
{
    /// <summary>
    /// Starts the editor. Arguments are files to open; <c>--screenshot file.png</c> draws about
    /// 120 frames without taking focus, saves a picture of the window and exits. When PadSharp is
    /// already running, the files go to that window instead and this process exits at once.
    /// </summary>
    [STAThread]
    public static int Main(string[] args) =>
        ForwardToRunningInstance(args) ? 0 : BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Used by the app and by design tools.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>
    /// True when another PadSharp owns the session and took the files. Otherwise this process claims the session
    /// (so the window it opens keeps tabs between runs) and starts normally. Screenshot runs never forward.
    /// </summary>
    internal static bool ForwardToRunningInstance(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Contains("--screenshot") || Session.TryClaim())
        {
            return false;
        }

        return SingleInstance.TrySend(SingleInstance.PipeName, args, TimeSpan.FromSeconds(3));
    }
}
