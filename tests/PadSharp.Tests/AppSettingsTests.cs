using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class AppSettingsTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void SettingsLiveInTheDataFolder() =>
        Assert.Equal(Path.Combine(sandbox.Data, "settings.json"), AppSettings.FilePath);

    [AvaloniaFact]
    public void AMissingFileGivesTheDefaults()
    {
        AppSettings loaded = AppSettings.Load();

        Assert.Equal(ThemeMode.Dark, loaded.Theme);
        Assert.Equal(Palettes.Accents[0].Name, loaded.Accent);
        Assert.Equal(Look.DefaultFontName, loaded.FontName);
        Assert.Equal(Look.DefaultTabWidth, loaded.TabWidth);
        Assert.True(loaded.ShowLineNumbers);
        Assert.False(loaded.WordWrap);
    }

    [AvaloniaFact]
    public void SavedSettingsLoadBack()
    {
        var settings = new AppSettings
        {
            Theme = ThemeMode.Light, Accent = "Ocean", FontName = "Consolas", FontSizePt = 14, TabWidth = 2,
            InsertSpaces = true, WordWrap = true, ShowLineNumbers = false, HighlightCurrentLine = false, ShowWhitespace = true,
        };

        settings.Save();
        AppSettings loaded = AppSettings.Load();

        Assert.Equal(
            (ThemeMode.Light, "Ocean", "Consolas", 14.0, 2, true, true, false, false, true),
            (loaded.Theme, loaded.Accent, loaded.FontName, loaded.FontSizePt, loaded.TabWidth, loaded.InsertSpaces,
             loaded.WordWrap, loaded.ShowLineNumbers, loaded.HighlightCurrentLine, loaded.ShowWhitespace));
    }

    [AvaloniaFact]
    public void SaveReportsSuccessOrWhyItFailed()
    {
        Assert.Null(new AppSettings().Save());
        Assert.Empty(Directory.EnumerateFiles(sandbox.Data, "*.tmp"));

        string blocker = Path.Combine(sandbox.Root, "blocked");
        File.WriteAllText(blocker, "file");
        AppSettings.DataFolder = blocker;
        Assert.NotNull(new AppSettings().Save());
    }

    [AvaloniaFact]
    public void WindowAndZoomValuesAreKeptInRange()
    {
        Directory.CreateDirectory(sandbox.Data);
        File.WriteAllText(AppSettings.FilePath, """{ "Zoom": 500, "WindowWidth": 5, "WindowHeight": 99999 }""");

        AppSettings loaded = AppSettings.Load();

        Assert.Equal((Look.MaxZoom, 400.0, 20000.0), (loaded.Zoom, loaded.WindowWidth, loaded.WindowHeight));
        Assert.Null(loaded.WindowX);
    }

    [AvaloniaFact]
    public void ADamagedFileGivesTheDefaults()
    {
        Directory.CreateDirectory(sandbox.Data);
        File.WriteAllText(AppSettings.FilePath, "{ not json");

        Assert.Equal(Look.DefaultFontSizePt, AppSettings.Load().FontSizePt);
    }

    [AvaloniaFact]
    public void HandEditedValuesAreKeptInRange()
    {
        Directory.CreateDirectory(sandbox.Data);
        File.WriteAllText(AppSettings.FilePath,
            """{ "Theme": 99, "Accent": "nope", "FontName": " ", "FontSizePt": 500, "TabWidth": 0 }""");

        AppSettings loaded = AppSettings.Load();

        Assert.Equal(ThemeMode.Dark, loaded.Theme);
        Assert.Equal(Palettes.Accents[0].Name, loaded.Accent);
        Assert.Equal(Look.DefaultFontName, loaded.FontName);
        Assert.Equal(AppSettings.MaxFontSizePt, loaded.FontSizePt);
        Assert.Equal(1, loaded.TabWidth);
    }
}
