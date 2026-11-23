using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace PadSharp.Tests;

public sealed class AppTests
{
    private static App Current => Assert.IsType<App>(Application.Current);

    [AvaloniaFact]
    public void TheAppLoadsFluentAndTheEditorStyles()
    {
        Assert.Contains(Current.Styles, s => s is FluentTheme);
        Assert.Contains(Current.Styles, s => s is EditorStyles);
    }

    [AvaloniaFact]
    public void SelectedTabsBlendIntoTheEditor()
    {
        Assert.Same(Look.EditorBack, Current.Resources["TabItemHeaderBackgroundSelected"]);
        Assert.Same(Look.MutedFore, Current.Resources["TabItemHeaderForegroundUnselected"]);
    }

    [AvaloniaFact]
    public void SetAccentChangesFluentsAccentInBothVariants()
    {
        FluentTheme fluent = Current.Styles.OfType<FluentTheme>().Single();
        Color before = fluent.Palettes[ThemeVariant.Dark].Accent;
        try
        {
            Current.SetAccent(Colors.Orange);

            Assert.Equal(Colors.Orange, fluent.Palettes[ThemeVariant.Dark].Accent);
            Assert.Equal(Colors.Orange, fluent.Palettes[ThemeVariant.Light].Accent);
        }
        finally
        {
            Current.SetAccent(before);
        }
    }
}
