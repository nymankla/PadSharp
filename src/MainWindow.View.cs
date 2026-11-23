using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;

namespace PadSharp;

/// <summary>Zoom, theme, accent, preferences and the About box; every preference change goes through OnSettingsChanged.</summary>
internal sealed partial class MainWindow
{
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
        if (screenshotPath is null && settings.Save() is { } problem)
        {
            transientMessage = $"Settings not saved: {problem}";
            UpdateStatus();
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

        app.RequestedThemeVariant = VariantFor(settings.Theme);
        Palette palette = app.ActualThemeVariant == ThemeVariant.Light ? Palettes.Light : Palettes.Dark;
        Accent accent = Palettes.AccentNamed(settings.Accent);
        CheckThemeMenus(accent);
        themeButton.Content = Icons.Make(palette.IsDark ? Icons.Sun : Icons.Moon);
        bool paletteChanged = palette != Look.Current;
        Look.Apply(palette, accent);
        (app as App)?.SetAccent(accent.From);

        // Syntax colours are built lazily from the palette, so only a palette change needs them rebuilt.
        if (paletteChanged)
        {
            SyntaxTheme.Reset();
        }

        documents.ForEach(d => d.RefreshTheme());
        InvalidateVisual();
    }

    /// <summary>The Fluent variant for a theme setting; System lets the operating system decide.</summary>
    internal static ThemeVariant VariantFor(ThemeMode mode) => mode switch
    {
        ThemeMode.Dark => ThemeVariant.Dark,
        ThemeMode.Light => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };

    private void CheckThemeMenus(Accent accent)
    {
        foreach ((ThemeMode mode, MenuItem item) in themeItems)
        {
            item.IsChecked = mode == settings.Theme;
        }

        foreach ((Accent variant, MenuItem item) in accentItems)
        {
            item.IsChecked = variant == accent;
        }
    }

    private void SetAccent(Accent accent)
    {
        settings.Accent = accent.Name;
        OnSettingsChanged();
    }

    /// <summary>The version from the project file, e.g. "0.1.0".</summary>
    internal static string Version => typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private Task<string?> ShowAboutAsync() =>
        Dialogs.AskAsync(this, "About " + AppName,
            $"{AppName} {Version}\nA small, fast scratchpad editor. Your tabs and unsaved text are kept between runs.\n\nMIT License.", "OK");
}
