using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class MainWindowTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    private static string[] Titles(MainWindow window) => [.. window.Documents.Select(d => d.Title)];

    private static void Select(MainWindow window, Document doc)
    {
        window.All<TabControl>().Single().SelectedItem = doc.Tab;
        Dispatcher.UIThread.RunJobs();
    }

    private static void Run(MainWindow window, string header)
    {
        window.All<MenuItem>().Single(m => m.Header as string == header).Command!.Execute(null);
        Dispatcher.UIThread.RunJobs();
    }

    private static Window Dialog(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        return Assert.Single(window.OwnedWindows);
    }

    private static void Click(Window dialog, string label)
    {
        dialog.All<Button>().Single(b => b.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void StartsWithOneUntitledTab()
    {
        MainWindow window = sandbox.Open();

        Assert.Equal(["Untitled-1"], Titles(window));
        Assert.Equal("Untitled-1 - PadSharp", window.Title);
    }

    [AvaloniaFact]
    public void FilesFromTheCommandLineReplaceTheEmptyTab()
    {
        string a = sandbox.File("a.txt", "A");
        string b = sandbox.File("b.md", "B");

        MainWindow window = sandbox.Open(files: [a, b, a]);

        Assert.Equal(["a.txt", "b.md"], Titles(window));
        Assert.Equal("a.txt", window.Current?.Title);
    }

    [AvaloniaFact]
    public void AMissingFileIsReported()
    {
        MainWindow window = sandbox.Open(files: [Path.Combine(sandbox.Files, "gone.txt")]);

        Window dialog = Dialog(window);
        Assert.Contains(dialog.All<TextBlock>(), t => t.Text?.Contains("doesn't exist", StringComparison.Ordinal) == true);
        Click(dialog, "OK");
        Assert.Equal(["Untitled-1"], Titles(window));
    }

    [AvaloniaFact]
    public void NewTabsTakeTheSmallestFreeNumber()
    {
        MainWindow window = sandbox.Open();
        window.Press(Key.N, RawInputModifiers.Control);
        window.Press(Key.N, RawInputModifiers.Control);
        Assert.Equal(["Untitled-1", "Untitled-2", "Untitled-3"], Titles(window));

        Select(window, window.Documents[1]);
        window.Press(Key.W, RawInputModifiers.Control);
        window.Press(Key.N, RawInputModifiers.Control);

        Assert.Equal(["Untitled-1", "Untitled-3", "Untitled-2"], Titles(window));
    }

    [AvaloniaFact]
    public void ClosingTheLastTabLeavesAFreshOne()
    {
        MainWindow window = sandbox.Open();
        Document first = window.Documents[0];

        window.Press(Key.W, RawInputModifiers.Control);

        Assert.NotSame(first, Assert.Single(window.Documents));
        Assert.Equal(["Untitled-1"], Titles(window));
    }

    [AvaloniaFact]
    public void ClosingAChangedTabAsksFirst()
    {
        MainWindow window = sandbox.Open();
        window.Documents[0].Editor.Document.Insert(0, "keep me");

        window.Press(Key.W, RawInputModifiers.Control);
        Window ask = Dialog(window);
        Assert.Contains(ask.All<TextBlock>(), t => t.Text == "Save changes to \"Untitled-1\"?");
        Click(ask, "Cancel");
        Assert.Equal("keep me", window.Documents[0].Text);

        window.Press(Key.W, RawInputModifiers.Control);
        Click(Dialog(window), "Don't Save");
        Assert.Equal(string.Empty, Assert.Single(window.Documents).Text);
    }

    [AvaloniaFact]
    public void CtrlTabCyclesThroughTabs()
    {
        MainWindow window = sandbox.Open();
        window.Press(Key.N, RawInputModifiers.Control);

        window.Press(Key.Tab, RawInputModifiers.Control);
        Assert.Equal("Untitled-1", window.Current?.Title);

        window.Press(Key.PageUp, RawInputModifiers.Control);
        Assert.Equal("Untitled-2", window.Current?.Title);
    }

    [AvaloniaFact]
    public void LineCommandsDuplicateAndDelete()
    {
        MainWindow window = sandbox.Open();
        Document doc = window.Current!;
        doc.Editor.Document.Text = "one\ntwo";
        doc.Editor.CaretOffset = 0;

        window.Press(Key.D, RawInputModifiers.Control);
        Assert.Equal("one\none\ntwo", doc.Text);

        window.Press(Key.L, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("one\ntwo", doc.Text);

        doc.Editor.CaretOffset = doc.Text.Length;
        window.Press(Key.L, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("one", doc.Text);
    }

    [AvaloniaFact]
    public void CaseCommandsConvertTheSelection()
    {
        MainWindow window = sandbox.Open();
        Document doc = window.Current!;
        doc.Editor.Document.Text = "say hello";
        doc.Editor.Select(4, 5);

        window.Press(Key.U, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("say HELLO", doc.Text);

        window.Press(Key.U, RawInputModifiers.Control);
        Assert.Equal("say hello", doc.Text);
    }

    [AvaloniaFact]
    public void ZoomChangesTheFontSizeAndResets()
    {
        MainWindow window = sandbox.Open();
        double normal = window.Current!.Editor.FontSize;

        window.Press(Key.Add, RawInputModifiers.Control);
        window.Press(Key.Add, RawInputModifiers.Control);
        Assert.Equal(normal + Look.PtToDip(2), window.Current.Editor.FontSize, 3);

        window.Press(Key.Subtract, RawInputModifiers.Control);
        Assert.Equal(normal + Look.PtToDip(1), window.Current.Editor.FontSize, 3);

        window.Press(Key.D0, RawInputModifiers.Control);
        Assert.Equal(normal, window.Current.Editor.FontSize, 3);
    }

    [AvaloniaFact]
    public void TheMainKeyboardZoomsToo()
    {
        MainWindow window = sandbox.Open();
        double normal = window.Current!.Editor.FontSize;

        window.Press(Key.OemPlus, RawInputModifiers.Control);
        window.Press(Key.OemPlus, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal(normal + Look.PtToDip(2), window.Current.Editor.FontSize, 3);

        window.Press(Key.OemMinus, RawInputModifiers.Control);
        Assert.Equal(normal + Look.PtToDip(1), window.Current.Editor.FontSize, 3);
    }

    [AvaloniaFact]
    public void ShortcutsUseTheCommandKeyOfThePlatform()
    {
        var save = KeyGesture.Parse("Ctrl+S");
        var tabs = KeyGesture.Parse("Ctrl+Shift+W");
        var f3 = KeyGesture.Parse("Shift+F3");

        Assert.Same(save, MainWindow.ForPlatform(save, KeyModifiers.Control));
        Assert.Equal(new KeyGesture(Key.S, KeyModifiers.Meta), MainWindow.ForPlatform(save, KeyModifiers.Meta));
        Assert.Equal(new KeyGesture(Key.W, KeyModifiers.Meta | KeyModifiers.Shift), MainWindow.ForPlatform(tabs, KeyModifiers.Meta));
        Assert.Same(f3, MainWindow.ForPlatform(f3, KeyModifiers.Meta));
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

    [AvaloniaFact]
    public void ToggleDarkLightSwitchesAndSavesTheTheme()
    {
        var settings = new AppSettings { Theme = ThemeMode.Dark };
        MainWindow window = sandbox.Open(settings);

        window.Press(Key.D, RawInputModifiers.Control | RawInputModifiers.Shift);

        Assert.Equal(ThemeMode.Light, settings.Theme);
        Assert.False(Look.Current.IsDark);
        Assert.Equal(ThemeMode.Light, AppSettings.Load().Theme);
    }

    [AvaloniaFact]
    public void AFailedSettingsSaveIsShown()
    {
        MainWindow window = sandbox.Open();
        string blocker = Path.Combine(sandbox.Root, "blocked");
        File.WriteAllText(blocker, "file");
        AppSettings.DataFolder = blocker;

        Run(window, "_Word wrap");

        Assert.StartsWith("Settings not saved:", window.StatusParts[0], StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AccentMenuChangesTheAccent()
    {
        var settings = new AppSettings();
        MainWindow window = sandbox.Open(settings);

        Run(window, "Sunset");

        Assert.Equal("Sunset", settings.Accent);
        Assert.Equal("Sunset", Look.CurrentAccent.Name);
        Assert.True(window.All<MenuItem>().Single(m => m.Header as string == "Sunset").IsChecked);
    }

    [AvaloniaFact]
    public void ViewMenuTogglesWordWrapLineNumbersAndAlwaysOnTop()
    {
        var settings = new AppSettings();
        MainWindow window = sandbox.Open(settings);

        Run(window, "_Word wrap");
        Run(window, "Show _Line Numbers");
        Run(window, "Always on _Top");

        Assert.True(settings.WordWrap && window.Current!.Editor.WordWrap);
        Assert.False(settings.ShowLineNumbers || window.Current.Gutter.IsVisible);
        Assert.True(window.Topmost);
    }

    [AvaloniaFact]
    public void LineEndingAndEncodingChoicesMarkTheTabChanged()
    {
        MainWindow window = sandbox.Open();

        Run(window, "Unix (LF)");
        Run(window, "UTF-16 LE");

        Document doc = window.Current!;
        Assert.Equal((EolKind.Unix, TextEncodingKind.Utf16LeBom, true), (doc.Eol, doc.Encoding, doc.IsDirty));
    }

    [AvaloniaFact]
    public void TheLanguageMenuIsOneFlatList()
    {
        MainWindow window = sandbox.Open();
        MenuItem menu = window.All<MenuItem>().Single(m => m.Header as string == "_Language");

        Assert.Equal(Languages.All.Count, menu.Items.Count);
        Assert.All(menu.Items.OfType<MenuItem>(), item => Assert.Empty(item.Items));

        Run(window, "Rust");
        Assert.Equal("Rust", window.Current!.Language.Name);
    }

    [AvaloniaFact]
    public void SaveWritesAFileTab()
    {
        string path = sandbox.File("s.txt", "old");
        MainWindow window = sandbox.Open(files: [path]);
        window.Current!.Editor.Document.Text = "new";

        window.Press(Key.S, RawInputModifiers.Control);

        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(window.Current.IsDirty);
    }

    [AvaloniaFact]
    public void ReloadReadsTheFileAgain()
    {
        string path = sandbox.File("r.txt", "one");
        MainWindow window = sandbox.Open(files: [path]);
        File.WriteAllText(path, "two");

        Run(window, "_Reload from Disk");

        Assert.Equal("two", window.Current!.Text);
    }

    [AvaloniaFact]
    public void ReloadAsksBeforeThrowingAwayChanges()
    {
        string path = sandbox.File("r.txt", "disk");
        MainWindow window = sandbox.Open(files: [path]);
        window.Current!.Editor.Document.Text = "mine";

        Run(window, "_Reload from Disk");
        Click(Dialog(window), "Cancel");
        Assert.Equal("mine", window.Current.Text);

        Run(window, "_Reload from Disk");
        Click(Dialog(window), "Reload");
        Assert.Equal("disk", window.Current.Text);
    }

    [AvaloniaFact]
    public void GoToLineMovesTheCaret()
    {
        MainWindow window = sandbox.Open();
        window.Current!.Editor.Document.Text = "1\n2\n3\n4";

        window.Press(Key.G, RawInputModifiers.Control);
        Window dialog = Dialog(window);
        dialog.All<NumericUpDown>().Single().Value = 3;
        Click(dialog, "Go");

        Assert.Equal(3, window.Current.Editor.TextArea.Caret.Line);
    }

    [AvaloniaFact]
    public void AboutAndPreferencesOpen()
    {
        MainWindow window = sandbox.Open();

        window.Press(Key.F1);
        Window about = Dialog(window);
        Assert.Equal("0.1.0", MainWindow.Version);
        Assert.Contains(about.All<TextBlock>(), t => t.Text?.StartsWith("PadSharp 0.1.0\n", StringComparison.Ordinal) == true);
        Click(about, "OK");

        window.Press(Key.OemComma, RawInputModifiers.Control);
        Assert.IsType<SettingsWindow>(Dialog(window));
    }
}
