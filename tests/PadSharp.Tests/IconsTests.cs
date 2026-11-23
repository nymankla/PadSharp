using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace PadSharp.Tests;

public sealed class IconsTests
{
    [AvaloniaFact]
    public void EveryIconPathParses()
    {
        string[] icons = [.. typeof(Icons).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)];

        Assert.NotEmpty(icons);
        foreach (string data in icons)
        {
            Avalonia.Controls.Shapes.Path icon = Icons.Make(data);
            Assert.NotNull(icon.Data);
            Assert.Equal((16, 16), (icon.Width, icon.Height));
            Assert.Same(Look.IconStroke, icon.Stroke);
        }
    }

    [AvaloniaFact]
    public void TheLogoIsATileWithTextLines()
    {
        Canvas logo = Icons.Logo(40);

        Assert.Equal((40, 40), (logo.Width, logo.Height));
        Assert.Equal(3, logo.Children.Count);
        Assert.Same(Look.LogoGradient, ((Rectangle)logo.Children[0]).Fill);
    }

    [AvaloniaFact]
    public void ASwatchShowsTheAccentGradient()
    {
        Accent accent = Palettes.AccentNamed("Ember");
        Ellipse swatch = Icons.Swatch(accent, 18);

        Assert.Equal(18, swatch.Width);
        var fill = Assert.IsType<LinearGradientBrush>(swatch.Fill);
        Assert.Equal([accent.From, accent.To], fill.GradientStops.Select(s => s.Color));
    }

    [AvaloniaFact]
    public void TheWindowIconRenders() => Assert.NotNull(Icons.CreateWindowIcon());
}
