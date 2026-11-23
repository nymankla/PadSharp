using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PadSharp;

/// <summary>
/// The Preferences window. Every change is applied and saved immediately through <c>changed</c>,
/// so there is no OK/Cancel: what you see is what is stored.
/// </summary>
internal sealed class SettingsWindow : Window
{
    private static readonly string[] FontCandidates =
    [
        "Cascadia Code", "Cascadia Mono", "JetBrains Mono", "Fira Code", "Consolas", "Source Code Pro",
        "Lucida Console", "Courier New", "Menlo", "SF Mono", "DejaVu Sans Mono", "Liberation Mono", "Ubuntu Mono",
    ];

    private static readonly int[] TabWidths = [2, 3, 4, 8];

    private readonly AppSettings settings;
    private readonly Action changed;

    public SettingsWindow(AppSettings settings, Action changed)
    {
        this.settings = settings;
        this.changed = changed;
        Title = "Preferences";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Look.PanelBack;

        Content = new StackPanel { Margin = new Thickness(20), Spacing = 16, Children = { Heading(), AppearanceCard(), EditorCard(), Footer() } };
    }

    private Border AppearanceCard() => Card("Appearance",
        Row("Theme", "Follow the system, or pick one", ThemePicker()),
        Row("Accent color", "Gradient, caret, selection and status bar", AccentPicker()),
        Row("Font", "Editor typeface", FontPicker()),
        Row("Font size", "In points, before zoom", FontSizePicker()));

    private Border EditorCard() => Card("Editor",
        Row("Tab width", "Columns per tab stop", TabWidthPicker()),
        Row("Insert spaces", "Typing Tab inserts spaces", Toggle(settings.InsertSpaces, v => settings.InsertSpaces = v)),
        Row("Word wrap", "Wrap long lines to the window", Toggle(settings.WordWrap, v => settings.WordWrap = v)),
        Row("Line numbers", "Show the line number margin", Toggle(settings.ShowLineNumbers, v => settings.ShowLineNumbers = v)),
        Row("Highlight current line", "Tint the line holding the caret", Toggle(settings.HighlightCurrentLine, v => settings.HighlightCurrentLine = v)),
        Row("Show whitespace", "Draw dots for spaces and arrows for tabs", Toggle(settings.ShowWhitespace, v => settings.ShowWhitespace = v)));

    /// <summary>"Changes apply and save instantly." and the Done button.</summary>
    private DockPanel Footer()
    {
        var close = new Button { Content = "Done", MinWidth = 96, IsDefault = true, IsCancel = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        close.Classes.Add("accent");
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Right);
        var note = new TextBlock { Text = "Changes apply and save instantly.", Foreground = Look.MutedFore, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
        return new DockPanel { Margin = new Thickness(0, 4, 0, 0), Children = { close, note } };
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private static StackPanel Heading() => new()
    {
        Spacing = 2,
        Children =
        {
            new TextBlock { Text = "Preferences", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Look.AccentGradient },
            new TextBlock { Text = AppSettings.FilePath, FontSize = 11, Foreground = Look.MutedFore, TextTrimming = TextTrimming.PathSegmentEllipsis },
        },
    };

    private static Border Card(string title, params Control[] rows)
    {
        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(new TextBlock
        {
            Text = title.ToUpperInvariant(),
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            LetterSpacing = 1.2,
            Foreground = Look.AccentGradient,
        });
        stack.Children.AddRange(rows);
        return new Border
        {
            Child = stack,
            Background = Look.RaisedBack,
            BorderBrush = Look.ChromeLine,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(18, 16),
        };
    }

    private static Grid Row(string label, string hint, Control control)
    {
        var text = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = label, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Look.EditorFore },
                new TextBlock { Text = hint, FontSize = 11.5, Foreground = Look.MutedFore },
            },
        };
        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16, Children = { text, control } };
    }

    private ToggleSwitch Toggle(bool value, Action<bool> set)
    {
        var toggle = new ToggleSwitch { IsChecked = value, OnContent = null, OffContent = null, MinWidth = 0 };
        toggle.IsCheckedChanged += (_, _) =>
        {
            set(toggle.IsChecked == true);
            changed();
        };
        return toggle;
    }

    private ComboBox ThemePicker()
    {
        var box = new ComboBox { Width = 170, ItemsSource = Enum.GetValues<ThemeMode>(), SelectedItem = settings.Theme };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is ThemeMode mode && mode != settings.Theme)
            {
                settings.Theme = mode;
                changed();
            }
        };
        return box;
    }

    /// <summary>One round swatch per accent; the chosen one wears a ring.</summary>
    private StackPanel AccentPicker()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var rings = new List<(Accent Accent, Border Ring)>();

        void MarkSelected()
        {
            foreach ((Accent accent, Border ring) in rings)
            {
                ring.BorderBrush = accent.Name == settings.Accent ? Look.EditorFore : Brushes.Transparent;
            }
        }

        foreach (Accent accent in Palettes.Accents)
        {
            Accent captured = accent;
            (Button button, Border ring) = Swatch(accent);
            button.Click += (_, _) =>
            {
                settings.Accent = captured.Name;
                MarkSelected();
                changed();
            };
            rings.Add((accent, ring));
            row.Children.Add(button);
        }

        MarkSelected();
        return row;
    }

    /// <summary>A round accent swatch inside a ring (coloured when chosen), as a button named after the accent.</summary>
    private static (Button Button, Border Ring) Swatch(Accent accent)
    {
        var ring = new Border { Child = Icons.Swatch(accent, 18), BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(13), Padding = new Thickness(2) };
        var button = new Button
        {
            Content = ring,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(13),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(button, accent.Name);
        return (button, ring);
    }

    private ComboBox FontPicker()
    {
        HashSet<string> installed = [.. FontManager.Current.SystemFonts.Select(f => f.Name)];
        List<string> names = [.. FontCandidates.Where(installed.Contains)];
        if (!names.Contains(settings.FontName))
        {
            names.Insert(0, settings.FontName);
        }

        var box = new ComboBox { Width = 170, ItemsSource = names, SelectedItem = settings.FontName };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is string name && name != settings.FontName)
            {
                settings.FontName = name;
                changed();
            }
        };
        return box;
    }

    private NumericUpDown FontSizePicker()
    {
        var box = new NumericUpDown
        {
            Width = 170,
            Minimum = (decimal)AppSettings.MinFontSizePt,
            Maximum = (decimal)AppSettings.MaxFontSizePt,
            Increment = 0.5m,
            FormatString = "0.#",
            Value = (decimal)settings.FontSizePt,
        };
        box.ValueChanged += (_, e) =>
        {
            if (e.NewValue is { } size)
            {
                settings.FontSizePt = (double)size;
                changed();
            }
        };
        return box;
    }

    private ComboBox TabWidthPicker()
    {
        List<int> widths = [.. TabWidths];
        if (!widths.Contains(settings.TabWidth))
        {
            widths.Add(settings.TabWidth);
            widths.Sort();
        }

        var box = new ComboBox { Width = 170, ItemsSource = widths, SelectedItem = settings.TabWidth };
        box.SelectionChanged += (_, _) =>
        {
            if (box.SelectedItem is int width && width != settings.TabWidth)
            {
                settings.TabWidth = width;
                changed();
            }
        };
        return box;
    }
}
