using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;

[assembly: AvaloniaTestApplication(typeof(PadSharp.Tests.TestApp))]

// Tests share static state (the data folder, the session lock, the palette), so they run one at a time.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PadSharp.Tests;

/// <summary>Runs the real <see cref="App"/> (Fluent theme, editor styles) on Avalonia's headless platform with Skia.</summary>
public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    /// <summary>Before any test: never let a test touch the person's real settings or session.</summary>
    [ModuleInitializer]
    internal static void KeepAwayFromRealData() =>
        AppSettings.DataFolder = Path.Combine(Path.GetTempPath(), "PadSharp.Tests", "default");
}

/// <summary>
/// A throwaway data folder (settings and session) and file folder for one test. Opens main windows on request and
/// on dispose closes them, releases the session and restores the default look.
/// </summary>
internal sealed class Sandbox : IDisposable
{
    private readonly string previousData = AppSettings.DataFolder;
    private readonly List<MainWindow> windows = [];

    public Sandbox()
    {
        Root = Path.Combine(Path.GetTempPath(), "PadSharp.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Files);
        AppSettings.DataFolder = Data;
    }

    public string Root { get; }

    public string Data => Path.Combine(Root, "data");

    public string Files => Path.Combine(Root, "files");

    /// <summary>Writes a file in the files folder and returns its full path.</summary>
    public string File(string name, string text)
    {
        string path = Path.Combine(Files, name);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    /// <summary>Opens and shows a main window like a normal start (no screenshot), with the given files.</summary>
    public MainWindow Open(AppSettings? settings = null, params string[] files)
    {
        var window = new MainWindow(files, screenshotPath: null, settings ?? new AppSettings(), _ => { });
        windows.Add(window);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    public void Dispose()
    {
        // Plain [Fact] tests run off the UI thread and never touch windows or brushes.
        if (Dispatcher.UIThread.CheckAccess())
        {
            foreach (MainWindow window in windows)
            {
                window.Close();
            }

            Dispatcher.UIThread.RunJobs();
            Look.Apply(Palettes.Dark, Palettes.Accents[0]);
            SyntaxTheme.Reset();
        }

        Session.Release();
        AppSettings.DataFolder = previousData;
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A file still held open on Windows; the temp folder is cleaned up eventually.
        }
    }
}

/// <summary>Keyboard and tree helpers for headless tests.</summary>
internal static class Ui
{
    /// <summary>Presses and releases a key, with modifiers, on whatever has focus in the window.</summary>
    public static void Press(this TopLevel window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();

        // The key may have closed the window (Escape in a dialog); a closed window takes no more input.
        if (window.IsVisible)
        {
            window.KeyRelease(key, modifiers, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Types text into whatever has focus.</summary>
    public static void Type(this TopLevel window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Every logical descendant of a type, in tree order.</summary>
    public static IEnumerable<T> All<T>(this ILogical root) => root.GetLogicalDescendants().OfType<T>();
}
