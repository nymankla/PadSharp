using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class MainWindowTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void StartsWithOneUntitledTab()
    {
        MainWindow window = sandbox.Open();

        Assert.Equal(["Untitled-1"], window.TabTitles());
        Assert.Equal("Untitled-1 - PadSharp", window.Title);
    }

    [AvaloniaFact]
    public void SizeMaximizedAndZoomAreRememberedBetweenRuns()
    {
        var settings = new AppSettings { WindowWidth = 900, WindowHeight = 600, Zoom = 3 };
        MainWindow first = sandbox.Open(settings);
        Assert.Equal((900, 600), (first.Width, first.Height));
        Assert.Equal(Look.PtToDip(Look.DefaultFontSizePt + 3), first.Current!.Editor.FontSize, 3);

        first.Press(Key.Add, RawInputModifiers.Control);
        first.WindowState = WindowState.Maximized;
        first.Close();
        Dispatcher.UIThread.RunJobs();

        AppSettings saved = AppSettings.Load();
        Assert.Equal((4, true), (saved.Zoom, saved.WindowMaximized));
        Assert.Equal((900, 600), (saved.WindowWidth, saved.WindowHeight));

        MainWindow second = sandbox.Open(saved);
        Assert.Equal(WindowState.Maximized, second.WindowState);
        Assert.Equal(Look.PtToDip(Look.DefaultFontSizePt + 4), second.Current!.Editor.FontSize, 3);
    }

    [AvaloniaFact]
    public void TheNormalSizeAndPositionAreSavedOnClose()
    {
        var settings = new AppSettings { WindowX = 120, WindowY = 80 };
        MainWindow window = sandbox.Open(settings);
        Assert.Equal(new PixelPoint(120, 80), window.Position);

        window.Width = 777;
        window.Height = 555;
        Dispatcher.UIThread.RunJobs();
        window.Close();
        Dispatcher.UIThread.RunJobs();

        AppSettings saved = AppSettings.Load();
        Assert.Equal((777, 555, false), (saved.WindowWidth, saved.WindowHeight, saved.WindowMaximized));
        Assert.Equal((120, 80), (saved.WindowX, saved.WindowY));
    }

}
