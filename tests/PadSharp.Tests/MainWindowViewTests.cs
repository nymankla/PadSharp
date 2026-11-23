using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace PadSharp.Tests;

public sealed class MainWindowViewTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void EachThemeSettingMapsToAFluentVariant()
    {
        Assert.Equal(Avalonia.Styling.ThemeVariant.Dark, MainWindow.VariantFor(ThemeMode.Dark));
        Assert.Equal(Avalonia.Styling.ThemeVariant.Light, MainWindow.VariantFor(ThemeMode.Light));
        Assert.Equal(Avalonia.Styling.ThemeVariant.Default, MainWindow.VariantFor(ThemeMode.System));
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

        window.RunMenu("_Word wrap");

        Assert.StartsWith("Settings not saved:", window.StatusParts[0], StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void AccentMenuChangesTheAccent()
    {
        var settings = new AppSettings();
        MainWindow window = sandbox.Open(settings);

        window.RunMenu("Sunset");

        Assert.Equal("Sunset", settings.Accent);
        Assert.Equal("Sunset", Look.CurrentAccent.Name);
        Assert.True(window.All<MenuItem>().Single(m => m.Header as string == "Sunset").IsChecked);
    }

    [AvaloniaFact]
    public void ViewMenuTogglesWordWrapLineNumbersAndAlwaysOnTop()
    {
        var settings = new AppSettings();
        MainWindow window = sandbox.Open(settings);

        window.RunMenu("_Word wrap");
        window.RunMenu("Show _Line Numbers");
        window.RunMenu("Always on _Top");

        Assert.True(settings.WordWrap && window.Current!.Editor.WordWrap);
        Assert.False(settings.ShowLineNumbers || window.Current.Gutter.IsVisible);
        Assert.True(window.Topmost);
    }

    [AvaloniaFact]
    public void AboutAndPreferencesOpen()
    {
        MainWindow window = sandbox.Open();

        window.Press(Key.F1);
        Window about = window.Dialog();
        Assert.Equal("0.1.0", MainWindow.Version);
        Assert.Contains(about.All<TextBlock>(), t => t.Text?.StartsWith("PadSharp 0.1.0\n", StringComparison.Ordinal) == true);
        about.Click("OK");

        window.Press(Key.OemComma, RawInputModifiers.Control);
        Assert.IsType<SettingsWindow>(window.Dialog());
    }
}
