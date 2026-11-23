using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Path = System.IO.Path;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace PadSharp;

/// <summary>The editor window: menus, toolbar, document tabs and status bar.</summary>
internal sealed class MainWindow : Window
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

    public MainWindow(IReadOnlyList<string> files, string? screenshotPath, AppSettings settings, Action<int> shutdown)
    {
        this.screenshotPath = screenshotPath;
        this.shutdown = shutdown;
        this.settings = settings;
        Title = AppName;
        Width = 1100;
        Height = 720;
        MinWidth = 400;
        MinHeight = 300;
        Icon = Icons.CreateWindowIcon();
        ShowActivated = screenshotPath is null;
        Background = Look.ChromeBack;

        var root = new DockPanel();
        Control menu = BuildTopBar();
        Control toolbar = BuildToolbar();
        Control statusBar = BuildStatusBar();
        DockPanel.SetDock(menu, Dock.Top);
        DockPanel.SetDock(toolbar, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        DockPanel.SetDock(findBar, Dock.Bottom);
        root.Children.AddRange([menu, toolbar, statusBar, findBar, tabs]);
        findBar.FindRequested += OnFindRequested;
        findBar.Closed += (_, _) => Current?.Editor.TextArea.Focus();
        Content = root;

        tabs.SelectionChanged += (_, _) =>
        {
            sessionPending = true;
            UpdateStatus();
            if (screenshotPath is null)
            {
                Current?.Editor.TextArea.Focus();
            }
        };

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver, RoutingStrategies.Tunnel);
        AddHandler(DragDrop.DropEvent, OnDrop, RoutingStrategies.Tunnel);
        frameTimer.Tick += OnFrameTick;

        ApplyTheme();
        if (Application.Current is { } app)
        {
            app.ActualThemeVariantChanged += (_, _) =>
            {
                if (settings.Theme == ThemeMode.System)
                {
                    ApplyTheme();
                }
            };
        }

        // Screenshot runs never touch the person's session.
        hotExit = screenshotPath is null && Session.TryClaim();
        if (hotExit)
        {
            RestoreSession();
            sessionTimer.Tick += (_, _) => SaveSessionIfPending();
            sessionTimer.Start();
        }

        if (screenshotPath is null)
        {
            Activated += (_, _) => _ = CheckDiskChangesAsync();
        }

        OpenFiles(files);
        if (documents.Count == 0)
        {
            NewDocument();
        }
    }

    private Document? Current => (tabs.SelectedItem as TabItem)?.Tag as Document;

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
            Current?.Editor.TextArea.Focus();
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
            _ = ConfirmCloseAsync();
            return;
        }

        settingsWindow?.Close();

        base.OnClosing(e);
    }

    private static string FormatNumber(long value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private static MenuItem Menu(string header, params Control[] items)
    {
        var menuItem = new MenuItem { Header = header };
        foreach (Control item in items)
        {
            menuItem.Items.Add(item);
        }

        return menuItem;
    }

    private MenuItem Item(string header, Action run, string? gesture = null, bool active = true)
    {
        var item = new MenuItem { Header = header, Command = new RelayCommand(run) };
        if (gesture is not null)
        {
            KeyGesture keys = KeyGesture.Parse(gesture);
            item.InputGesture = keys;
            if (active)
            {
                shortcuts.Add((keys, run));
            }
        }

        return item;
    }

    private Menu BuildMenu()
    {
        wordWrapItem = Item("_Word wrap", () => SetWordWrap(!settings.WordWrap));
        wordWrapItem.ToggleType = MenuItemToggleType.CheckBox;
        lineNumbersItem = Item("Show _Line Numbers", () => SetLineNumbers(!settings.ShowLineNumbers));
        lineNumbersItem.ToggleType = MenuItemToggleType.CheckBox;

        foreach (ThemeMode mode in Enum.GetValues<ThemeMode>())
        {
            ThemeMode captured = mode;
            MenuItem item = Item(mode == ThemeMode.System ? "Follow _System" : $"_{mode}", () => SetTheme(captured));
            item.ToggleType = MenuItemToggleType.Radio;
            themeItems[mode] = item;
        }

        foreach (Accent accent in Palettes.Accents)
        {
            Accent captured = accent;
            MenuItem item = Item(accent.Name, () => SetAccent(captured));
            item.ToggleType = MenuItemToggleType.Radio;
            item.Icon = Icons.Swatch(accent, 14);
            accentItems[accent] = item;
        }
        alwaysOnTopItem = Item("Always on _Top", () =>
        {
            Topmost = !Topmost;
            alwaysOnTopItem.IsChecked = Topmost;
        });
        alwaysOnTopItem.ToggleType = MenuItemToggleType.CheckBox;

        foreach (EolKind eol in new[] { EolKind.Windows, EolKind.Unix, EolKind.Macintosh })
        {
            EolKind captured = eol;
            MenuItem item = Item(TextFile.EolName(eol), () => SetEol(captured));
            item.ToggleType = MenuItemToggleType.Radio;
            eolItems[eol] = item;
        }

        foreach (TextEncodingKind enc in Enum.GetValues<TextEncodingKind>())
        {
            TextEncodingKind captured = enc;
            MenuItem item = Item(TextFile.EncodingName(enc), () => SetEncoding(captured));
            item.ToggleType = MenuItemToggleType.Radio;
            encodingItems[enc] = item;
        }

        var menu = new Menu { Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
        menu.Items.Add(Menu("_File",
            Item("_New", NewDocument, "Ctrl+N"),
            Item("_Open...", () => _ = OpenWithDialogAsync(), "Ctrl+O"),
            Item("_Reload from Disk", () => _ = ReloadCurrentAsync()),
            Item("_Save", () => _ = SaveDocumentAsync(Current), "Ctrl+S"),
            Item("Save _As...", () => _ = SaveDocumentAsAsync(Current), "Ctrl+Alt+S"),
            Item("Sa_ve All", () => _ = SaveAllAsync(), "Ctrl+Shift+S"),
            Item("_Close", () => _ = CloseDocumentAsync(Current), "Ctrl+W"),
            Item("Clos_e All", () => _ = CloseAllAsync(), "Ctrl+Shift+W"),
            new Separator(),
            Item("E_xit", Close, "Alt+F4", active: false)));
        menu.Items.Add(Menu("_Edit",
            Item("_Undo", () => Current?.Editor.Undo(), "Ctrl+Z", active: false),
            Item("_Redo", () => Current?.Editor.Redo(), "Ctrl+Y", active: false),
            new Separator(),
            Item("Cu_t", () => Current?.Editor.Cut(), "Ctrl+X", active: false),
            Item("_Copy", () => Current?.Editor.Copy(), "Ctrl+C", active: false),
            Item("_Paste", () => Current?.Editor.Paste(), "Ctrl+V", active: false),
            Item("_Delete", () => Current?.Editor.Delete(), "Delete", active: false),
            Item("Select A_ll", () => Current?.Editor.SelectAll(), "Ctrl+A", active: false),
            new Separator(),
            Menu("Convert Case to",
                Item("_UPPERCASE", () => ConvertCase(upper: true), "Ctrl+Shift+U"),
                Item("_lowercase", () => ConvertCase(upper: false), "Ctrl+U")),
            Menu("Line Operations",
                Item("Duplicate Current Line", DuplicateLine, "Ctrl+D"),
                Item("Delete Current Line", DeleteLine, "Ctrl+Shift+L")),
            Menu("Line _Endings", [.. eolItems.Values])));
        menu.Items.Add(Menu("_Search",
            Item("_Find", () => ShowFind(replace: false), "Ctrl+F"),
            Item("Find _Next", () => FindAgain(backward: false), "F3"),
            Item("Find _Previous", () => FindAgain(backward: true), "Shift+F3"),
            Item("_Replace", () => ShowFind(replace: true), "Ctrl+H"),
            new Separator(),
            Item("_Go to Line...", () => _ = GoToLineAsync(), "Ctrl+G")));
        menu.Items.Add(Menu("_View",
            alwaysOnTopItem,
            new Separator(),
            Menu("_Zoom",
                Item("Zoom _In", () => ChangeZoom(1), "Ctrl+Add"),
                Item("Zoom _Out", () => ChangeZoom(-1), "Ctrl+Subtract"),
                Item("_Reset Zoom", () => SetZoom(0), "Ctrl+D0")),
            new Separator(),
            wordWrapItem,
            lineNumbersItem));
        menu.Items.Add(Menu("E_ncoding", [.. encodingItems.Values]));
        menu.Items.Add(BuildLanguageMenu());
        menu.Items.Add(Menu("_Settings",
            Item("_Preferences...", ShowSettings, "Ctrl+OemComma"),
            new Separator(),
            Menu("_Theme", [.. themeItems.Values]),
            Menu("_Accent Color", [.. accentItems.Values]),
            Item("Toggle _Dark/Light", ToggleDarkLight, "Ctrl+Shift+D")));
        menu.Items.Add(Menu("_?", Item("_About " + AppName, () => _ = ShowAboutAsync(), "F1")));
        return menu;
    }

    /// <summary>Language menu: one flat list; the language is normally detected, this is the override.</summary>
    private MenuItem BuildLanguageMenu()
    {
        var root = new MenuItem { Header = "_Language" };
        foreach (Language language in Languages.All)
        {
            Language captured = language;
            var item = new MenuItem
            {
                // Escape underscores so names are not read as access keys.
                Header = language.Name.Replace("_", "__", StringComparison.Ordinal),
                Command = new RelayCommand(() => SetLanguage(captured)),
                ToggleType = MenuItemToggleType.Radio,
            };
            languageItems[language] = item;
            root.Items.Add(item);
        }

        return root;
    }

    /// <summary>Logo and app name in gradient, then the menu.</summary>
    private Border BuildTopBar()
    {
        var name = new TextBlock
        {
            Text = AppName,
            FontWeight = FontWeight.Bold,
            FontSize = 13,
            Foreground = Look.AccentGradient,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 10, 0),
        };
        Canvas logo = Icons.Logo(20);
        logo.VerticalAlignment = VerticalAlignment.Center;
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 4, 0, 2), Children = { logo, name, BuildMenu() } };
        return new Border { Child = row, Background = Look.ChromeBack };
    }

    private static Button ToolButton(Panel bar, string icon, string tip, Action run)
    {
        var button = new Button
        {
            Content = Icons.Make(icon),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(7),
            Margin = new Thickness(1, 0),
            CornerRadius = new CornerRadius(6),
            Focusable = false,
        };
        button.Click += (_, _) => run();
        ToolTip.SetTip(button, tip);
        bar.Children.Add(button);
        return button;
    }

    private static void ToolSeparator(Panel bar) =>
        bar.Children.Add(new Border { Width = 1, Margin = new Thickness(6, 7), Background = Look.ChromeLine });

    private Border BuildToolbar()
    {
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 2, 8, 6) };
        ToolButton(bar, Icons.New, "New", NewDocument);
        ToolButton(bar, Icons.Open, "Open", () => _ = OpenWithDialogAsync());
        saveButton = ToolButton(bar, Icons.Save, "Save", () => _ = SaveDocumentAsync(Current));
        ToolButton(bar, Icons.SaveAll, "Save All", () => _ = SaveAllAsync());
        ToolButton(bar, Icons.Close, "Close", () => _ = CloseDocumentAsync(Current));
        ToolSeparator(bar);
        ToolButton(bar, Icons.Cut, "Cut", () => Current?.Editor.Cut());
        ToolButton(bar, Icons.Copy, "Copy", () => Current?.Editor.Copy());
        ToolButton(bar, Icons.Paste, "Paste", () => Current?.Editor.Paste());
        ToolSeparator(bar);
        undoButton = ToolButton(bar, Icons.Undo, "Undo", () => Current?.Editor.Undo());
        redoButton = ToolButton(bar, Icons.Redo, "Redo", () => Current?.Editor.Redo());
        ToolSeparator(bar);
        ToolButton(bar, Icons.Find, "Find", () => ShowFind(replace: false));
        ToolButton(bar, Icons.Replace, "Replace", () => ShowFind(replace: true));
        ToolSeparator(bar);
        ToolButton(bar, Icons.ZoomIn, "Zoom In", () => ChangeZoom(1));
        ToolButton(bar, Icons.ZoomOut, "Zoom Out", () => ChangeZoom(-1));
        ToolSeparator(bar);
        ToolButton(bar, Icons.Wrap, "Word wrap", () => SetWordWrap(!settings.WordWrap));
        ToolSeparator(bar);
        themeButton = ToolButton(bar, Icons.Sun, "Toggle dark/light mode (Ctrl+Shift+D)", ToggleDarkLight);
        ToolButton(bar, Icons.Sliders, "Preferences (Ctrl+,)", ShowSettings);
        return new Border { Child = bar, Background = Look.ChromeBack };
    }

    /// <summary>Language, position, line count, line endings, encoding. Language, line endings and encoding open a picker on click.</summary>
    private Border BuildStatusBar()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto") };
        (TextBlock Text, Action? Click)[] parts =
        [
            (languageText, () => ShowPicker(languageText, Languages.All, l => l.Name, l => l == Current?.Language, SetLanguage)),
            (positionText, null),
            (linesText, null),
            (eolText, () => ShowPicker(eolText, [EolKind.Windows, EolKind.Unix, EolKind.Macintosh], TextFile.EolName, k => k == Current?.Eol, SetEol)),
            (encodingText, () => ShowPicker(encodingText, Enum.GetValues<TextEncodingKind>(), TextFile.EncodingName, k => k == Current?.Encoding, SetEncoding)),
        ];
        var divider = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        for (int i = 0; i < parts.Length; i++)
        {
            (TextBlock text, Action? click) = parts[i];
            text.FontSize = 12;
            text.Foreground = Brushes.White;
            text.VerticalAlignment = VerticalAlignment.Center;
            var cell = new Border
            {
                Child = text,
                Padding = new Thickness(12, 5),
                MinWidth = i == 0 ? 0 : 70,
                Background = Brushes.Transparent,
                BorderBrush = divider,
                BorderThickness = new Thickness(i == 0 ? 0 : 1, 0, 0, 0),
                Margin = new Thickness(0, 3),
            };
            if (click is not null)
            {
                cell.Cursor = new Cursor(StandardCursorType.Hand);
                cell.PointerPressed += (_, _) => click();
            }

            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        languageText.FontWeight = FontWeight.SemiBold;
        return new Border { Child = grid, Background = Look.StatusGradient };
    }

    /// <summary>A pop-up list above a status-bar cell, the current choice checked.</summary>
    private static void ShowPicker<T>(Control anchor, IEnumerable<T> choices, Func<T, string> name, Func<T, bool> isCurrent, Action<T> pick)
    {
        var menu = new ContextMenu { Placement = PlacementMode.TopEdgeAlignedLeft };
        foreach (T choice in choices)
        {
            T captured = choice;
            menu.Items.Add(new MenuItem
            {
                Header = name(choice).Replace("_", "__", StringComparison.Ordinal),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = isCurrent(choice),
                Command = new RelayCommand(() => pick(captured)),
            });
        }

        menu.Open(anchor);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Key == Key.Tab || e.Key == Key.PageDown || e.Key == Key.PageUp) && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            bool back = e.Key == Key.PageUp || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            CycleTab(back ? -1 : 1);
            e.Handled = true;
            return;
        }

        foreach ((KeyGesture gesture, Action run) in shortcuts)
        {
            if (gesture.Matches(e))
            {
                run();
                e.Handled = true;
                return;
            }
        }
    }

    private void CycleTab(int step)
    {
        int count = tabs.Items.Count;
        if (count > 1)
        {
            tabs.SelectedIndex = (tabs.SelectedIndex + step + count) % count;
        }
    }

    private Document AddDocument(Document doc)
    {
        documents.Add(doc);
        TextEditor editor = doc.Editor;
        doc.ApplySettings(settings, zoom);
        doc.Changed += (_, _) =>
        {
            sessionPending = true;
            if (doc == Current)
            {
                UpdateStatus();
            }
        };
        doc.CloseRequested += (_, _) => _ = CloseDocumentAsync(doc);
        editor.TextArea.Caret.PositionChanged += (_, _) => UpdateStatus();
        editor.TextArea.SelectionChanged += (_, _) => UpdateStatus();
        editor.TextArea.KeyUp += (_, _) => UpdateStatus();
        editor.AddHandler(PointerWheelChangedEvent, (_, e) =>
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                ChangeZoom(e.Delta.Y > 0 ? 1 : -1);
                e.Handled = true;
            }
        }, RoutingStrategies.Tunnel);
        editor.ContextMenu = BuildEditorMenu();
        tabs.Items.Add(doc.Tab);
        return doc;
    }

    private ContextMenu BuildEditorMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem { Header = "Cut", Command = new RelayCommand(() => Current?.Editor.Cut()) });
        menu.Items.Add(new MenuItem { Header = "Copy", Command = new RelayCommand(() => Current?.Editor.Copy()) });
        menu.Items.Add(new MenuItem { Header = "Paste", Command = new RelayCommand(() => Current?.Editor.Paste()) });
        menu.Items.Add(new MenuItem { Header = "Delete", Command = new RelayCommand(() => Current?.Editor.Delete()) });
        menu.Items.Add(new MenuItem { Header = "Select All", Command = new RelayCommand(() => Current?.Editor.SelectAll()) });
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "UPPERCASE", Command = new RelayCommand(() => ConvertCase(upper: true)) });
        menu.Items.Add(new MenuItem { Header = "lowercase", Command = new RelayCommand(() => ConvertCase(upper: false)) });
        return menu;
    }

    private int NextUntitledNumber()
    {
        int n = 1;
        while (documents.Exists(d => d.UntitledNumber == n))
        {
            n++;
        }

        return n;
    }

    private void NewDocument()
    {
        Document doc = AddDocument(new Document(NextUntitledNumber()));
        tabs.SelectedItem = doc.Tab;
        UpdateStatus();
    }

    private static FilePickerFileType FileTypeOf(Language language) =>
        new($"{language.Name} (*.{string.Join(";*.", language.Extensions)})") { Patterns = language.Patterns };

    /// <summary>"All types" then one filter per language; <paramref name="first"/>, when given, leads the list.</summary>
    private static FilePickerFileType[] FileTypes(Language? first = null)
    {
        FilePickerFileType all = new("All types (*.*)") { Patterns = ["*"] };
        IEnumerable<FilePickerFileType> rest = Languages.All.Where(l => l != first).Select(FileTypeOf);
        return first is null ? [all, .. rest] : [FileTypeOf(first), all, .. rest];
    }

    private async Task OpenWithDialogAsync()
    {
        IReadOnlyList<IStorageFile> picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = true,
            FileTypeFilter = FileTypes(),
        });
        OpenFiles(picked.Select(f => f.TryGetLocalPath()).OfType<string>().ToList());
    }

    private void OpenFiles(IReadOnlyList<string> paths)
    {
        foreach (string raw in paths)
        {
            string path = Path.GetFullPath(raw);
            Document? existing = documents.Find(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                tabs.SelectedItem = existing.Tab;
                continue;
            }

            if (!File.Exists(path))
            {
                _ = Dialogs.AskAsync(this, "Open", $"\"{path}\" doesn't exist.", "OK");
                continue;
            }

            Document? pristine = documents.Count == 1 && documents[0].IsPristineUntitled ? documents[0] : null;
            var doc = new Document(0);
            try
            {
                doc.LoadFrom(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _ = Dialogs.AskAsync(this, "Open", $"Cannot open file \"{path}\".\n{ex.Message}", "OK");
                continue;
            }

            AddDocument(doc);
            tabs.SelectedItem = doc.Tab;
            if (pristine is not null)
            {
                RemoveDocument(pristine);
            }
        }

        UpdateStatus();
    }

    private async Task ReloadCurrentAsync()
    {
        if (Current is not { FilePath: { } path } doc)
        {
            return;
        }

        if (doc.IsDirty && await Dialogs.AskAsync(this, "Reload",
                $"Reload \"{doc.Title}\" from disk and lose your changes?", "Reload", "Cancel") != "Reload")
        {
            return;
        }

        try
        {
            doc.LoadFrom(path, detectLanguage: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.AskAsync(this, "Reload", ex.Message, "OK");
        }
    }

    private async Task<bool> SaveDocumentAsync(Document? doc)
    {
        if (doc is null)
        {
            return false;
        }

        return doc.FilePath is null ? await SaveDocumentAsAsync(doc) : await WriteDocumentAsync(doc, doc.FilePath);
    }

    private async Task<bool> SaveDocumentAsAsync(Document? doc)
    {
        if (doc is null)
        {
            return false;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = doc.FilePath is null ? doc.Title : Path.GetFileName(doc.FilePath),
            FileTypeChoices = FileTypes(doc.Language),
            DefaultExtension = doc.Language.Extensions[0],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath() is { } path && await WriteDocumentAsync(doc, path);
    }

    private async Task<bool> WriteDocumentAsync(Document doc, string path)
    {
        try
        {
            doc.SaveTo(path);
            UpdateStatus();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.AskAsync(this, "Save failed", $"Cannot save file \"{path}\".\n{ex.Message}", "OK");
            return false;
        }
    }

    private async Task SaveAllAsync()
    {
        foreach (Document doc in documents.ToList())
        {
            if (doc.IsDirty)
            {
                tabs.SelectedItem = doc.Tab;
                if (!await SaveDocumentAsync(doc))
                {
                    return;
                }
            }
        }
    }

    /// <summary>Asks whether to save a changed document. False means the person cancelled.</summary>
    private async Task<bool> ConfirmDiscardAsync(Document doc)
    {
        tabs.SelectedItem = doc.Tab;
        string? answer = await Dialogs.AskAsync(this, AppName, $"Save changes to \"{doc.FullName}\"?", "Save", "Don't Save", "Cancel");
        return answer switch
        {
            "Save" => await SaveDocumentAsync(doc),
            "Don't Save" => true,
            _ => false,
        };
    }

    private async Task ConfirmCloseAsync()
    {
        foreach (Document doc in documents.ToList())
        {
            if (doc.IsDirty && !await ConfirmDiscardAsync(doc))
            {
                return;
            }
        }

        closeConfirmed = true;
        Close();
    }

    private async Task<bool> CloseDocumentAsync(Document? doc)
    {
        if (doc is null)
        {
            return false;
        }

        if (doc.IsDirty && !await ConfirmDiscardAsync(doc))
        {
            return false;
        }

        RemoveDocument(doc);
        if (documents.Count == 0)
        {
            NewDocument();
        }

        UpdateStatus();
        return true;
    }

    private async Task CloseAllAsync()
    {
        foreach (Document doc in documents.ToList())
        {
            if (!await CloseDocumentAsync(doc))
            {
                return;
            }
        }
    }

    private void RemoveDocument(Document doc)
    {
        tabs.Items.Remove(doc.Tab);
        documents.Remove(doc);
        savedRevisions.Remove(doc);
        sessionPending = true;
    }

    /// <summary>Reopens the tabs of the last run: unsaved text from the buffers folder, the rest from disk.</summary>
    private void RestoreSession()
    {
        if (Session.Load() is not { } state)
        {
            return;
        }

        foreach (SessionTab tab in state.Tabs)
        {
            // A saved language is a manual choice; without one the language is detected again.
            Language? chosen = Languages.All.FirstOrDefault(l => l.Name == tab.Language);
            Document doc;
            if (tab.Buffer is { } buffer && Session.ReadBuffer(buffer) is { } text)
            {
                doc = new Document(tab.Path is null ? Math.Max(1, tab.UntitledNumber) : 0);
                doc.Restore(tab.Path, text, tab.Encoding, tab.Eol, chosen ?? Languages.Detect(tab.Path, text), tab.Caret);
            }
            else if (tab.Path is { } path && File.Exists(path))
            {
                doc = new Document(0);
                try
                {
                    doc.LoadFrom(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (chosen is not null)
                {
                    doc.Language = chosen;
                }

                doc.Editor.CaretOffset = Math.Clamp(tab.Caret, 0, doc.Editor.Document.TextLength);
            }
            else
            {
                continue;
            }

            AddDocument(doc);
        }

        if (documents.Count > 0)
        {
            tabs.SelectedIndex = Math.Clamp(state.Selected, 0, documents.Count - 1);
        }
    }

    private void SaveSessionIfPending()
    {
        if (sessionPending)
        {
            SaveSession();
        }
    }

    /// <summary>Writes every tab to the session; unsaved text goes to the buffers folder, rewritten only when it changed.</summary>
    private void SaveSession()
    {
        sessionPending = false;
        var state = new SessionState();
        var changed = new List<(string Name, string Text)>();
        foreach (Document doc in tabs.Items.OfType<TabItem>().Select(t => t.Tag).OfType<Document>())
        {
            if (doc.IsPristineUntitled)
            {
                continue;
            }

            var entry = new SessionTab
            {
                Path = doc.FilePath,
                UntitledNumber = doc.UntitledNumber,
                Encoding = doc.Encoding,
                Eol = doc.Eol,
                // Only a manual choice is stored, so a later, better detection (e.g. a new language) takes effect.
                Language = doc.Language == Languages.Detect(doc.FilePath, doc.Text) ? null : doc.Language.Name,
                Caret = doc.Editor.CaretOffset,
            };
            if (doc.IsDirty)
            {
                entry.Buffer = doc.BufferName;
                if (!savedRevisions.TryGetValue(doc, out int revision) || revision != doc.Revision)
                {
                    changed.Add((doc.BufferName, doc.Text));
                    savedRevisions[doc] = doc.Revision;
                }
            }
            else
            {
                // Its buffer file is deleted by this save; a later change must write it again.
                savedRevisions.Remove(doc);
            }

            if (doc == Current)
            {
                state.Selected = state.Tabs.Count;
            }

            state.Tabs.Add(entry);
        }

        Session.Save(state, changed);
    }

    /// <summary>
    /// On activation: files changed elsewhere are reloaded silently, unless they have unsaved changes here,
    /// in which case the person chooses.
    /// </summary>
    private async Task CheckDiskChangesAsync()
    {
        if (checkingDisk)
        {
            return;
        }

        checkingDisk = true;
        try
        {
            foreach (Document doc in documents.ToList())
            {
                if (!doc.IsChangedOnDisk || doc.FilePath is not { } path)
                {
                    continue;
                }

                if (doc.IsDirty)
                {
                    doc.AcceptDiskVersion();
                    tabs.SelectedItem = doc.Tab;
                    string? answer = await Dialogs.AskAsync(this, AppName,
                        $"\"{doc.Title}\" was changed outside {AppName}. Reload it and lose your changes?", "Reload", "Keep Mine");
                    if (answer != "Reload")
                    {
                        continue;
                    }
                }

                try
                {
                    doc.LoadFrom(path, detectLanguage: false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    doc.AcceptDiskVersion();
                }
            }
        }
        finally
        {
            checkingDisk = false;
        }

        UpdateStatus();
    }

    private void ConvertCase(bool upper)
    {
        if (Current?.Editor is not { SelectionLength: > 0 } editor)
        {
            return;
        }

        int start = editor.SelectionStart;
        string selected = editor.SelectedText;
        string converted = upper ? selected.ToUpper(CultureInfo.CurrentCulture) : selected.ToLower(CultureInfo.CurrentCulture);
        editor.Document.Replace(start, selected.Length, converted);
        editor.Select(start, converted.Length);
    }

    private void DuplicateLine()
    {
        if (Current is not { } doc)
        {
            return;
        }

        TextDocument text = doc.Editor.Document;
        DocumentLine line = text.GetLineByOffset(doc.Editor.CaretOffset);
        string newline = TextUtilities.GetNewLineFromDocument(text, line.LineNumber);
        text.Insert(line.EndOffset, newline + text.GetText(line.Offset, line.Length));
    }

    private void DeleteLine()
    {
        if (Current is not { } doc)
        {
            return;
        }

        TextDocument text = doc.Editor.Document;
        DocumentLine line = text.GetLineByOffset(doc.Editor.CaretOffset);
        int start = line.Offset;
        int length = line.TotalLength;
        if (line.DelimiterLength == 0 && line.PreviousLine is { } previous)
        {
            start = previous.EndOffset;
            length = text.TextLength - start;
        }

        text.Remove(start, length);
    }

    private void SetEol(EolKind eol)
    {
        if (Current is { } doc && doc.Eol != eol)
        {
            doc.Eol = eol;
            doc.MarkDirty();
        }
    }

    private void SetEncoding(TextEncodingKind encoding)
    {
        if (Current is { } doc && doc.Encoding != encoding)
        {
            doc.Encoding = encoding;
            doc.MarkDirty();
        }
    }

    private void SetLanguage(Language language)
    {
        if (Current is { } doc)
        {
            doc.Language = language;
        }

        UpdateStatus();
    }

    private void ShowFind(bool replace) => findBar.Open(replace, Current?.Editor.SelectedText ?? string.Empty);

    private void FindAgain(bool backward)
    {
        if (findBar.Query.Length == 0)
        {
            ShowFind(replace: false);
            return;
        }

        if (Current is { } doc)
        {
            Find(doc, findBar.Query, findBar.Options with { Backward = backward }, incremental: false);
        }
    }

    /// <summary>Shows a find result in the bar, or in the status bar when the bar is closed (F3).</summary>
    private void Report(string message, bool problem)
    {
        if (findBar.IsVisible)
        {
            findBar.SetStatus(message, problem);
        }
        else if (message.Length > 0)
        {
            transientMessage = message;
            UpdateStatus();
        }
    }

    /// <summary>"3 of 12" when <paramref name="at"/> is a match, otherwise the total.</summary>
    private void ReportMatches(Document doc, string query, SearchOptions options, int? at)
    {
        List<int> matches = TextSearch.Matches(doc.Text, query, options);
        int index = at is { } i ? matches.BinarySearch(i) : -1;
        string message = matches.Count == 0 ? "No results"
            : index >= 0 ? $"{FormatNumber(index + 1)} of {FormatNumber(matches.Count)}"
            : matches.Count == 1 ? "1 match" : $"{FormatNumber(matches.Count)} matches";
        Report(message, matches.Count == 0);
    }

    private static void SelectAndShow(TextEditor editor, int start, int length)
    {
        editor.Select(start, length);
        editor.TextArea.Caret.BringCaretToView();
    }

    /// <summary>Selects the next match. Incremental searches start at the selection start, so typing extends the match in place.</summary>
    private void Find(Document doc, string query, SearchOptions options, bool incremental)
    {
        TextEditor editor = doc.Editor;
        int from = incremental ? editor.SelectionStart : editor.SelectionStart + editor.SelectionLength;
        if (TextSearch.Find(doc.Text, query, editor.SelectionStart, from, options) is { } found)
        {
            SelectAndShow(editor, found.Index, found.Length);
            ReportMatches(doc, query, options, found.Index);
        }
        else
        {
            Report("No results", problem: true);
        }
    }

    private void OnFindRequested(object? sender, FindRequestEventArgs e)
    {
        if (Current is not { } doc)
        {
            return;
        }

        if (e.Query.Length == 0)
        {
            findBar.SetStatus(string.Empty, problem: false);
            return;
        }

        TextEditor editor = doc.Editor;
        switch (e.Action)
        {
            case FindAction.Incremental:
            case FindAction.Next:
                Find(doc, e.Query, e.Options, incremental: e.Action == FindAction.Incremental);
                break;

            case FindAction.Previous:
                Find(doc, e.Query, e.Options with { Backward = true }, incremental: false);
                break;

            case FindAction.Replace:
                if (TextSearch.IsMatchAt(doc.Text, e.Query, editor.SelectionStart, editor.SelectionLength, e.Options))
                {
                    int start = editor.SelectionStart;
                    editor.Document.Replace(start, editor.SelectionLength, e.Replacement);
                    editor.Select(start + e.Replacement.Length, 0);
                }

                Find(doc, e.Query, e.Options, incremental: false);
                break;

            case FindAction.ReplaceAll:
                (string text, int total) = TextSearch.ReplaceAll(doc.Text, e.Query, e.Replacement, e.Options);
                if (total > 0)
                {
                    doc.ReplaceAllText(text);
                }

                Report(total == 0 ? "No results" : $"Replaced {FormatNumber(total)}", problem: total == 0);
                break;

            default:
                break;
        }
    }

    private async Task GoToLineAsync()
    {
        if (Current is not { } doc)
        {
            return;
        }

        int? line = await Dialogs.GoToLineAsync(this, doc.LineOf(doc.Editor.CaretOffset), doc.LineCount);
        if (line is { } target)
        {
            doc.Editor.TextArea.Caret.Line = target;
            doc.Editor.TextArea.Caret.Column = 1;
            doc.Editor.Select(doc.Editor.CaretOffset, 0);
            doc.Editor.TextArea.Caret.BringCaretToView();
            doc.Editor.TextArea.Focus();
        }
    }

    private void ChangeZoom(int delta) => SetZoom(zoom + delta);

    private void SetZoom(int level)
    {
        zoom = Math.Clamp(level, Look.MinZoom, Look.MaxZoom);
        ApplyEditorSettings();
    }

    private void SetWordWrap(bool on)
    {
        settings.WordWrap = on;
        OnSettingsChanged();
    }

    private void SetLineNumbers(bool on)
    {
        settings.ShowLineNumbers = on;
        OnSettingsChanged();
    }

    private void SetTheme(ThemeMode mode)
    {
        settings.Theme = mode;
        OnSettingsChanged();
    }

    /// <summary>Flips to the opposite of what is on screen now (System counts as whatever it resolves to).</summary>
    private void ToggleDarkLight() => SetTheme(Look.Current.IsDark ? ThemeMode.Light : ThemeMode.Dark);

    private void ShowSettings()
    {
        if (settingsWindow is not null)
        {
            settingsWindow.Activate();
            return;
        }

        settingsWindow = new SettingsWindow(settings, OnSettingsChanged);
        settingsWindow.Closed += (_, _) =>
        {
            settingsWindow = null;
            Current?.Editor.TextArea.Focus();
        };
        settingsWindow.Show(this);
    }

    /// <summary>Applies every preference to the window and all documents, then saves them.</summary>
    private void OnSettingsChanged()
    {
        ApplyTheme();
        ApplyEditorSettings();
        if (screenshotPath is null)
        {
            settings.Save();
        }
    }

    private void ApplyEditorSettings()
    {
        wordWrapItem.IsChecked = settings.WordWrap;
        lineNumbersItem.IsChecked = settings.ShowLineNumbers;
        foreach (Document doc in documents)
        {
            doc.ApplySettings(settings, zoom);
        }

        UpdateStatus();
    }

    /// <summary>Sets the Fluent light/dark variant and recolours the shared brushes and syntax colours to match.</summary>
    private void ApplyTheme()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        app.RequestedThemeVariant = settings.Theme switch
        {
            ThemeMode.Dark => ThemeVariant.Dark,
            ThemeMode.Light => ThemeVariant.Light,
            _ => ThemeVariant.Default,
        };
        Palette palette = app.ActualThemeVariant == ThemeVariant.Light ? Palettes.Light : Palettes.Dark;
        Accent accent = Palettes.AccentNamed(settings.Accent);
        foreach ((ThemeMode mode, MenuItem item) in themeItems)
        {
            item.IsChecked = mode == settings.Theme;
        }

        foreach ((Accent variant, MenuItem item) in accentItems)
        {
            item.IsChecked = variant == accent;
        }

        themeButton.Content = Icons.Make(palette.IsDark ? Icons.Sun : Icons.Moon);
        bool paletteChanged = palette != Look.Current;
        Look.Apply(palette, accent);
        (app as App)?.SetAccent(accent.From);

        // Syntax colours are built lazily from the palette, so only a palette change needs them rebuilt.
        if (paletteChanged)
        {
            SyntaxTheme.Reset();
        }

        foreach (Document doc in documents)
        {
            doc.RefreshTheme();
        }

        InvalidateVisual();
    }

    private void SetAccent(Accent accent)
    {
        settings.Accent = accent.Name;
        OnSettingsChanged();
    }

    private Task<string?> ShowAboutAsync() =>
        Dialogs.AskAsync(this, "About " + AppName, $"{AppName}\nA small, fast scratchpad editor. Your tabs and unsaved text are kept between runs.", "OK");

    private void UpdateStatus()
    {
        if (Current is not { } doc)
        {
            return;
        }

        TextEditor editor = doc.Editor;
        int length = editor.Document.TextLength;
        int caret = Math.Min(editor.CaretOffset, length);
        int selected = Math.Min(editor.SelectionLength, length - Math.Min(editor.SelectionStart, length));
        string position = $"Ln {FormatNumber(doc.LineOf(caret))}, Col {FormatNumber(doc.ColumnOf(caret))}";
        if (selected > 0)
        {
            position += $"  ({FormatNumber(selected)} selected)";
        }

        if (editor.TextArea.OverstrikeMode)
        {
            position += "  OVR";
        }

        positionText.Text = position;
        linesText.Text = doc.LineCount == 1 ? "1 line" : $"{FormatNumber(doc.LineCount)} lines";
        eolText.Text = TextFile.EolShortName(doc.Eol);
        encodingText.Text = TextFile.EncodingName(doc.Encoding);
        languageText.Text = transientMessage ?? doc.Language.Name;
        transientMessage = null;

        foreach ((Language language, MenuItem item) in languageItems)
        {
            item.IsChecked = language == doc.Language;
        }

        foreach ((EolKind eol, MenuItem item) in eolItems)
        {
            item.IsChecked = eol == doc.Eol;
        }

        foreach ((TextEncodingKind enc, MenuItem item) in encodingItems)
        {
            item.IsChecked = enc == doc.Encoding;
        }

        saveButton.Opacity = doc.IsDirty ? 1 : 0.3;
        undoButton.Opacity = editor.CanUndo ? 1 : 0.3;
        redoButton.Opacity = editor.CanRedo ? 1 : 0.3;
        Title = $"{(doc.IsDirty ? "*" : string.Empty)}{doc.FullName} - {AppName}";
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 })
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } items)
        {
            OpenFiles(items.Select(i => i.TryGetLocalPath()).OfType<string>().ToList());
            e.Handled = true;
        }
    }

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
