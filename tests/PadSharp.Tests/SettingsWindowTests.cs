using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PadSharp.Tests;

public sealed class SettingsWindowTests : IDisposable
{
    private readonly Sandbox sandbox = new();
    private readonly AppSettings settings = new();
    private readonly SettingsWindow window;
    private int changes;

    public SettingsWindowTests()
    {
        window = new SettingsWindow(settings, () => changes++);
        window.Show();
    }

    public void Dispose()
    {
        window.Close();
        sandbox.Dispose();
    }

    [AvaloniaFact]
    public void ShowsWhereSettingsAreStored() =>
        Assert.Contains(window.All<TextBlock>(), t => t.Text == AppSettings.FilePath);

    [AvaloniaFact]
    public void TogglesChangeTheirSettingAndApplyAtOnce()
    {
        ToggleSwitch[] toggles = [.. window.All<ToggleSwitch>()];
        Assert.Equal(5, toggles.Length);

        foreach (ToggleSwitch toggle in toggles)
        {
            toggle.IsChecked = toggle.IsChecked != true;
        }

        Assert.Equal((true, true, false, false, true),
            (settings.InsertSpaces, settings.WordWrap, settings.ShowLineNumbers, settings.HighlightCurrentLine, settings.ShowWhitespace));
        Assert.Equal(5, changes);
    }

    [AvaloniaFact]
    public void PickersChangeThemeFontSizeAndTabWidth()
    {
        ComboBox[] boxes = [.. window.All<ComboBox>()];
        boxes.Single(b => b.ItemsSource is ThemeMode[]).SelectedItem = ThemeMode.Light;
        boxes.Single(b => b.ItemsSource is List<int>).SelectedItem = 8;
        window.All<NumericUpDown>().Single().Value = 13.5m;

        Assert.Equal((ThemeMode.Light, 8, 13.5), (settings.Theme, settings.TabWidth, settings.FontSizePt));
        Assert.Equal(3, changes);
    }

    [AvaloniaFact]
    public void TheFontListKeepsTheCurrentFontEvenIfNotInstalled()
    {
        settings.FontName = "Imaginary Mono";
        using var other = new Sandbox();
        var second = new SettingsWindow(settings, () => { });

        ComboBox fonts = second.All<ComboBox>().Single(b => b.ItemsSource is List<string>);

        Assert.Equal("Imaginary Mono", fonts.SelectedItem);
    }

    [AvaloniaFact]
    public void ASwatchPicksTheAccent()
    {
        Button ocean = window.All<Button>().Single(b => (string?)ToolTip.GetTip(b) == "Ocean");

        ocean.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(("Ocean", 1), (settings.Accent, changes));
    }

    [AvaloniaFact]
    public void EscapeAndDoneClose()
    {
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        window.Press(Key.Escape);

        Assert.True(closed);

        var again = new SettingsWindow(settings, () => { });
        again.Show();
        again.All<Button>().Single(b => b.Content as string == "Done").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(again.IsVisible);
    }
}
