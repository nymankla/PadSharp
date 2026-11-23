using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Path = System.IO.Path;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace PadSharp;

/// <summary>The editor window: menus, toolbar, document tabs and status bar.</summary>
internal sealed partial class MainWindow : Window
{
    private const string AppName = "PadSharp";
    private const int ScreenshotFrames = 120;

    private readonly List<Document> documents = [];
    private readonly List<(KeyGesture Gesture, Action Run)> shortcuts = [];
    private readonly TabControl tabs = new() { Padding = new Thickness(0), Background = Look.ChromeBack };
    private readonly TextBlock languageText = new();
    private readonly TextBlock positionText = new();
    private readonly TextBlock linesText = new();
    private readonly TextBlock eolText = new();
    private readonly TextBlock encodingText = new();
    private readonly FindBar findBar = new();
    private readonly DispatcherTimer sessionTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Dictionary<Document, int> savedRevisions = [];
    private readonly Dictionary<EolKind, MenuItem> eolItems = [];
    private readonly Dictionary<TextEncodingKind, MenuItem> encodingItems = [];
    private readonly Dictionary<Language, MenuItem> languageItems = [];
    private readonly DispatcherTimer frameTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly string? screenshotPath;
    private readonly Action<int> shutdown;

    private readonly AppSettings settings;
    private readonly Dictionary<ThemeMode, MenuItem> themeItems = [];
    private readonly Dictionary<Accent, MenuItem> accentItems = [];

    private MenuItem wordWrapItem = null!;
    private MenuItem lineNumbersItem = null!;
    private MenuItem alwaysOnTopItem = null!;
    private Button saveButton = null!;
    private Button undoButton = null!;
    private Button redoButton = null!;
    private Button themeButton = null!;
    private SettingsWindow? settingsWindow;
    private int zoom;
    private bool closeConfirmed;
    private int frames;
    private string? transientMessage;

    /// <summary>True when this instance owns the session: closing saves every tab instead of asking.</summary>
    private readonly bool hotExit;
    private bool sessionPending;
    private bool checkingDisk;

    /// <summary>Why the last session save failed (shown in the status bar until one works), or null.</summary>
    private string? sessionProblem;

    /// <summary>Messages raised before the window is shown (e.g. a missing file on the command line).</summary>
    private readonly List<(string Title, string Message)> pendingMessages = [];

    public MainWindow(IReadOnlyList<string> files, string? screenshotPath, AppSettings settings, Action<int> shutdown)
    {
        this.screenshotPath = screenshotPath;
        this.shutdown = shutdown;
        this.settings = settings;
        Title = AppName;
        Icon = Icons.CreateWindowIcon();
        ShowActivated = screenshotPath is null;
        Background = Look.ChromeBack;
        PlaceWindow();
        Content = BuildLayout();
        WireEvents();
        ApplyTheme();
        FollowSystemTheme();

        // Screenshot runs never touch the person's session.
        // Program.Main usually claimed the session already; a window opened another way claims it here.
        hotExit = screenshotPath is null && Session.TryTakeOwnership();
        if (hotExit)
        {
            StartHotExit();
        }

        OpenFiles(files);
        if (documents.Count == 0)
        {
            NewDocument();
        }
    }

    /// <summary>Size, position, maximized state and zoom from settings. Screenshot runs keep 1100x720 with no zoom.</summary>
    private void PlaceWindow()
    {
        MinWidth = 400;
        MinHeight = 300;
        bool remember = screenshotPath is null;
        Width = remember ? settings.WindowWidth : 1100;
        Height = remember ? settings.WindowHeight : 720;
        zoom = remember ? settings.Zoom : 0;
        if (remember && settings.WindowX is { } x && settings.WindowY is { } y)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(x, y);
        }

        if (remember && settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    /// <summary>Menu and toolbar on top, status bar and find bar at the bottom, the tabs in between.</summary>
    private DockPanel BuildLayout()
    {
        Control menu = BuildTopBar();
        Control toolbar = BuildToolbar();
        Control statusBar = BuildStatusBar();
        DockPanel.SetDock(menu, Dock.Top);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        DockPanel.SetDock(findBar, Dock.Bottom);
        findBar.FindRequested += OnFindRequested;
        findBar.Closed += (_, _) => Current?.Editor.TextArea.Focus();
        return new DockPanel { Children = { menu, toolbar, statusBar, findBar, tabs } };
    }

    private void WireEvents()
    {
        tabs.SelectionChanged += (_, _) => OnTabChanged();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Tunnel);
        frameTimer.Tick += OnFrameTick;
        if (screenshotPath is null)
        {
            Activated += (_, _) => Run(() => CheckDiskChangesAsync());
        }
    }

    private void OnTabChanged()
    {
        sessionPending = true;
        UpdateStatus();
        if (screenshotPath is null)
        {
            Current?.Editor.TextArea.Focus();
        }
    }

    /// <summary>With the System theme, a light/dark switch in the operating system recolours PadSharp too.</summary>
    private void FollowSystemTheme()
    {
        if (Application.Current is { } app)
        {
            app.ActualThemeVariantChanged += OnSystemThemeChanged;
        }
    }

    private void OnSystemThemeChanged(object? sender, EventArgs e)
    {
        if (settings.Theme == ThemeMode.System)
        {
            ApplyTheme();
        }
    }

    /// <summary>
    /// For the window that owns the session: restore the tabs, keep saving them, save once more on a crash, and take
    /// files from later starts.
    /// </summary>
    private void StartHotExit()
    {
        RestoreSession();
        sessionTimer.Tick += (_, _) => SaveSessionIfPending();
        sessionTimer.Start();
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        var instanceServer = new SingleInstanceServer(SingleInstance.PipeName,
            files => Dispatcher.UIThread.Post(() => OpenFromOtherInstance(files)));
        Closed += (_, _) => instanceServer.Dispose();
    }

    /// <summary>The tab in front.</summary>
    internal Document? Current => (tabs.SelectedItem as TabItem)?.Tag as Document;

    /// <summary>Every open tab, in the order they were opened.</summary>
    internal IReadOnlyList<Document> Documents => documents;

    /// <summary>The find bar (hidden until Ctrl+F / Ctrl+H).</summary>
    internal FindBar FindBar => findBar;

    /// <summary>The status-bar texts, left to right: language, position, lines, line endings, encoding.</summary>
    internal IReadOnlyList<string> StatusParts =>
        [.. new[] { languageText, positionText, linesText, eolText, encodingText }.Select(t => t.Text ?? string.Empty)];

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (screenshotPath is not null)
        {
            WindowZOrder.SendToBack(this);
            frameTimer.Start();
        }
        else
        {
            KeepOnScreen();
            Current?.Editor.TextArea.Focus();
        }

        foreach ((string title, string message) in pendingMessages)
        {
            _ = Dialogs.AskAsync(this, title, message, "OK");
        }

        pendingMessages.Clear();
    }

    private void OnUnhandledException(object? sender, EventArgs e) => SaveSessionBeforeCrash();

    /// <summary>Files from a later start (e.g. <c>ed notes.txt</c>): open them here and come to the front.</summary>
    internal void OpenFromOtherInstance(IReadOnlyList<string> files)
    {
        OpenFiles(files);
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
    }

    /// <summary>Starts an async command; an error it throws is shown instead of vanishing.</summary>
    private void Run(Func<Task> command) => _ = ObserveAsync(command);

    private async Task ObserveAsync(Func<Task> command)
    {
        try
        {
            await command();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            ShowMessage(AppName, $"That didn't work: {ex.Message}");
        }
    }

    /// <summary>Shows a message now, or once the window is open (a dialog needs a visible owner).</summary>
    private void ShowMessage(string title, string message)
    {
        if (IsVisible)
        {
            _ = Dialogs.AskAsync(this, title, message, "OK");
        }
        else
        {
            pendingMessages.Add((title, message));
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (hotExit)
        {
            SaveSession();
        }
        else if (screenshotPath is null && !closeConfirmed && documents.Exists(d => d.IsDirty))
        {
            e.Cancel = true;
            Run(() => ConfirmCloseAsync());
            return;
        }

        if (screenshotPath is null)
        {
            RememberWindow();
        }

        settingsWindow?.Close();

        base.OnClosing(e);
    }

    /// <summary>Keeps size, position, maximized state and zoom for the next start.</summary>
    private void RememberWindow()
    {
        settings.Zoom = zoom;
        settings.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            settings.WindowWidth = ClientSize.Width;
            settings.WindowHeight = ClientSize.Height;
            settings.WindowX = Position.X;
            settings.WindowY = Position.Y;
        }

        _ = settings.Save();
    }

    /// <summary>A remembered position on a screen that is gone (unplugged monitor) moves back onto the main screen.</summary>
    private void KeepOnScreen()
    {
        if (Screens.All.Count == 0 || Screens.ScreenFromPoint(Position) is not null || Screens.Primary is not { } primary)
        {
            return;
        }

        PixelRect area = primary.WorkingArea;
        Position = new PixelPoint(area.X + 40, area.Y + 40);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        sessionTimer.Stop();
        if (hotExit)
        {
            Dispatcher.UIThread.UnhandledException -= OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            Session.Release();
        }
    }

    private static string FormatNumber(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void OnFrameTick(object? sender, EventArgs e)
    {
        frames++;
        InvalidateVisual();
        if (frames < ScreenshotFrames || screenshotPath is null)
        {
            return;
        }

        frameTimer.Stop();
        int code = 0;
        try
        {
            SaveWindowPicture(screenshotPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine(ex.Message);
            code = 1;
        }

        shutdown(code);
    }

    private void SaveWindowPicture(string path)
    {
        string? folder = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        double scale = RenderScaling;
        var size = new PixelSize(Math.Max(1, (int)(Bounds.Width * scale)), Math.Max(1, (int)(Bounds.Height * scale)));
        using var bitmap = new RenderTargetBitmap(size, new Vector(96 * scale, 96 * scale));
        bitmap.Render(this);
        bitmap.Save(path, new PngBitmapEncoderOptions());
    }
}
