using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PadSharp;

/// <summary>Menus, the toolbar and keyboard shortcuts (Ctrl on Windows and Linux, Cmd on macOS).</summary>
internal sealed partial class MainWindow
{
    private static MenuItem Menu(string header, params Control[] items)
    {
        var menuItem = new MenuItem { Header = header };
        foreach (Control item in items)
        {
            menuItem.Items.Add(item);
        }

        return menuItem;
    }

    /// <summary>The platform's command key: Ctrl, or Cmd on macOS.</summary>
    private static KeyModifiers CommandKey =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>Shortcuts are written with Ctrl; on a platform whose command key differs (macOS: Cmd) Ctrl becomes it.</summary>
    internal static KeyGesture ForPlatform(KeyGesture gesture, KeyModifiers command)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        return command == KeyModifiers.Control || !gesture.KeyModifiers.HasFlag(KeyModifiers.Control)
            ? gesture
            : new KeyGesture(gesture.Key, (gesture.KeyModifiers & ~KeyModifiers.Control) | command);
    }

    /// <summary>A shortcut as this platform shows it, for tooltips.</summary>
    private static string ShortcutText(string gesture) => ForPlatform(KeyGesture.Parse(gesture), CommandKey).ToString("p", null);

    /// <summary>A shortcut with no menu item of its own (e.g. a second key for the same command).</summary>
    private void Bind(string gesture, Action run) => shortcuts.Add((ForPlatform(KeyGesture.Parse(gesture), CommandKey), run));

    private MenuItem Item(string header, Action run, string? gesture = null, bool active = true)
    {
        var item = new MenuItem { Header = header, Command = new RelayCommand(run) };
        if (gesture is not null)
        {
            KeyGesture keys = ForPlatform(KeyGesture.Parse(gesture), CommandKey);
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
        var menu = new Menu { Background = Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center };
        menu.Items.Add(FileMenu());
        menu.Items.Add(EditMenu());
        menu.Items.Add(SearchMenu());
        menu.Items.Add(ViewMenu());
        menu.Items.Add(Menu("E_ncoding", Radios(Enum.GetValues<TextEncodingKind>(), TextFile.EncodingName, SetEncoding, encodingItems)));
        menu.Items.Add(BuildLanguageMenu());
        menu.Items.Add(SettingsMenu());
        menu.Items.Add(Menu("_?", Item("_About " + AppName, () => Run(() => ShowAboutAsync()), "F1")));

        // Zoom from the main keyboard too, not only the numpad: Ctrl+= / Ctrl++ (Shift on some layouts) and Ctrl+-.
        Bind("Ctrl+OemPlus", () => ChangeZoom(1));
        Bind("Ctrl+Shift+OemPlus", () => ChangeZoom(1));
        Bind("Ctrl+OemMinus", () => ChangeZoom(-1));
        return menu;
    }

    /// <summary>A menu item with a check mark.</summary>
    private MenuItem Check(string header, Action run)
    {
        MenuItem item = Item(header, run);
        item.ToggleType = MenuItemToggleType.CheckBox;
        return item;
    }

    /// <summary>One radio item per value; <paramref name="items"/> keeps them so the check can follow the tab in front.</summary>
    private Control[] Radios<T>(IEnumerable<T> values, Func<T, string> header, Action<T> pick, Dictionary<T, MenuItem> items)
        where T : notnull
    {
        foreach (T value in values)
        {
            T captured = value;
            MenuItem item = Item(header(value), () => pick(captured));
            item.ToggleType = MenuItemToggleType.Radio;
            items[value] = item;
        }

        return [.. items.Values];
    }

    private MenuItem FileMenu() => Menu("_File",
        Item("_New", NewDocument, "Ctrl+N"),
        Item("_Open...", () => Run(() => OpenWithDialogAsync()), "Ctrl+O"),
        Item("_Reload from Disk", () => Run(() => ReloadCurrentAsync())),
        Item("_Save", () => Run(() => SaveDocumentAsync(Current)), "Ctrl+S"),
        Item("Save _As...", () => Run(() => SaveDocumentAsAsync(Current)), "Ctrl+Alt+S"),
        Item("Sa_ve All", () => Run(() => SaveAllAsync()), "Ctrl+Shift+S"),
        Item("_Close", () => Run(() => CloseDocumentAsync(Current)), "Ctrl+W"),
        Item("Clos_e All", () => Run(() => CloseAllAsync()), "Ctrl+Shift+W"),
        new Separator(),
        Item("E_xit", Close, "Alt+F4", active: false));

    /// <summary>Undo, clipboard and Select All are handled by the editor itself, so their shortcuts are only shown here.</summary>
    private MenuItem EditMenu() => Menu("_Edit",
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
        Menu("Line _Endings", Radios([EolKind.Windows, EolKind.Unix, EolKind.Macintosh], TextFile.EolName, SetEol, eolItems)));

    private MenuItem SearchMenu() => Menu("_Search",
        Item("_Find", () => ShowFind(replace: false), "Ctrl+F"),
        Item("Find _Next", () => FindAgain(backward: false), "F3"),
        Item("Find _Previous", () => FindAgain(backward: true), "Shift+F3"),
        Item("_Replace", () => ShowFind(replace: true), "Ctrl+H"),
        new Separator(),
        Item("_Go to Line...", () => Run(() => GoToLineAsync()), "Ctrl+G"));

    private MenuItem ViewMenu()
    {
        alwaysOnTopItem = Check("Always on _Top", () =>
        {
            Topmost = !Topmost;
            alwaysOnTopItem.IsChecked = Topmost;
        });
        wordWrapItem = Check("_Word wrap", () => SetWordWrap(!settings.WordWrap));
        lineNumbersItem = Check("Show _Line Numbers", () => SetLineNumbers(!settings.ShowLineNumbers));
        return Menu("_View",
            alwaysOnTopItem,
            new Separator(),
            Menu("_Zoom",
                Item("Zoom _In", () => ChangeZoom(1), "Ctrl+Add"),
                Item("Zoom _Out", () => ChangeZoom(-1), "Ctrl+Subtract"),
                Item("_Reset Zoom", () => SetZoom(0), "Ctrl+D0")),
            new Separator(),
            wordWrapItem,
            lineNumbersItem);
    }

    private MenuItem SettingsMenu()
    {
        Control[] themes = Radios(Enum.GetValues<ThemeMode>(), m => m == ThemeMode.System ? "Follow _System" : $"_{m}", SetTheme, themeItems);
        Control[] accents = Radios(Palettes.Accents, a => a.Name, SetAccent, accentItems);
        foreach ((Accent accent, MenuItem item) in accentItems)
        {
            item.Icon = Icons.Swatch(accent, 14);
        }

        return Menu("_Settings",
            Item("_Preferences...", ShowSettings, "Ctrl+OemComma"),
            new Separator(),
            Menu("_Theme", themes),
            Menu("_Accent Color", accents),
            Item("Toggle _Dark/Light", ToggleDarkLight, "Ctrl+Shift+D"));
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
        ToolButton(bar, Icons.Open, "Open", () => Run(() => OpenWithDialogAsync()));
        saveButton = ToolButton(bar, Icons.Save, "Save", () => Run(() => SaveDocumentAsync(Current)));
        ToolButton(bar, Icons.SaveAll, "Save All", () => Run(() => SaveAllAsync()));
        ToolButton(bar, Icons.Close, "Close", () => Run(() => CloseDocumentAsync(Current)));
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
        themeButton = ToolButton(bar, Icons.Sun, $"Toggle dark/light mode ({ShortcutText("Ctrl+Shift+D")})", ToggleDarkLight);
        ToolButton(bar, Icons.Sliders, $"Preferences ({ShortcutText("Ctrl+OemComma")})", ShowSettings);
        return new Border { Child = bar, Background = Look.ChromeBack };
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
}
