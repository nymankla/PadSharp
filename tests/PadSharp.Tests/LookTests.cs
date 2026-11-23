using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace PadSharp.Tests;

public sealed class LookTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void ApplyRecoloursTheSharedBrushesInPlace()
    {
        SolidColorBrush editorBack = Look.EditorBack;
        Accent ocean = Palettes.AccentNamed("Ocean");

        Look.Apply(Palettes.Light, ocean);

        Assert.Same(editorBack, Look.EditorBack);
        Assert.Equal(Palettes.Light.Editor, Look.EditorBack.Color);
        Assert.Equal(Palettes.Light.Fore, Look.EditorFore.Color);
        Assert.Equal(ocean.To, Look.Caret.Color);
        Assert.Equal(ocean.StatusTo, Look.GutterActiveFore.Color);
        Assert.Equal([ocean.StatusFrom, ocean.StatusTo], Look.StatusGradient.GradientStops.Select(s => s.Color));
        Assert.Equal([ocean.From, ocean.To], Look.AccentGradient.GradientStops.Select(s => s.Color));
        Assert.Same(Palettes.Light, Look.Current);
        Assert.Same(ocean, Look.CurrentAccent);
        Assert.Same(Palettes.Light.Syntax, Look.Syntax);
    }

    [AvaloniaFact]
    public void DarkThemesTintTheSelectionMoreThanLightOnes()
    {
        Look.Apply(Palettes.Dark, Palettes.Accents[0]);
        byte dark = Look.SelectionBack.Color.A;
        Look.Apply(Palettes.Light, Palettes.Accents[0]);

        Assert.True(dark > Look.SelectionBack.Color.A);
    }

    [AvaloniaFact]
    public void ZoomAddsOnePointPerStepButNeverGoesBelowTwo()
    {
        Assert.Equal(13.5, Look.FontSizeForZoomPt(10.5, 3));
        Assert.Equal(2, Look.FontSizeForZoomPt(10.5, Look.MinZoom));
    }

    [AvaloniaFact]
    public void PointsConvertToDeviceIndependentPixels() => Assert.Equal(96, Look.PtToDip(72));

    [AvaloniaFact]
    public void TheChosenFontComesFirstWithFallbacks()
    {
        string[] families = [.. Look.EditorFontFor("Fira Code").FamilyNames];
        Assert.Equal("Fira Code", families[0]);
        Assert.Contains("Courier New", families);
    }

    [AvaloniaFact]
    public void AccentsAreFoundByNameWithTheFirstAsDefault()
    {
        Assert.Equal("Ocean", Palettes.AccentNamed("ocean").Name);
        Assert.Same(Palettes.Accents[0], Palettes.AccentNamed("no such accent"));
        Assert.Same(Palettes.Accents[0], Palettes.AccentNamed(null));
        Assert.Equal(Palettes.Accents.Count, Palettes.Accents.Select(a => a.Name).Distinct().Count());
    }

    [AvaloniaFact]
    public void PalettesKnowWhetherTheyAreDark()
    {
        Assert.True(Palettes.Dark.IsDark);
        Assert.False(Palettes.Light.IsDark);
        Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), Palettes.Rgb(0x123456));
    }
}
