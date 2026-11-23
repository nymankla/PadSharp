using System.Text.Json;
using System.Text.Json.Serialization;

namespace PadSharp;

/// <summary>The person's preferences, kept as JSON in the user's application-data folder.</summary>
internal sealed class AppSettings
{
    public const double MinFontSizePt = 6;
    public const double MaxFontSizePt = 36;

    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    /// <summary>Name of the accent variant (see <see cref="Palettes.Accents"/>).</summary>
    public string Accent { get; set; } = Palettes.Accents[0].Name;

    public string FontName { get; set; } = Look.DefaultFontName;

    public double FontSizePt { get; set; } = Look.DefaultFontSizePt;

    public int TabWidth { get; set; } = Look.DefaultTabWidth;

    public bool InsertSpaces { get; set; }

    public bool WordWrap { get; set; }

    public bool ShowLineNumbers { get; set; } = true;

    public bool HighlightCurrentLine { get; set; } = true;

    public bool ShowWhitespace { get; set; }

    /// <summary>Where settings are stored, e.g. %APPDATA%\PadSharp\settings.json on Windows.</summary>
    public static string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadSharp", "settings.json");

    /// <summary>Reads the saved settings; a missing or damaged file gives the defaults.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                AppSettings? loaded = JsonSerializer.Deserialize(File.ReadAllText(FilePath), SettingsJson.Default.AppSettings);
                if (loaded is not null)
                {
                    loaded.Sanitize();
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Settings not loaded: {ex.Message}");
        }

        return new AppSettings();
    }

    /// <summary>Writes the settings; failures are reported on the error stream and otherwise ignored.</summary>
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SettingsJson.Default.AppSettings));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Settings not saved: {ex.Message}");
        }
    }

    /// <summary>Keeps hand-edited values inside sensible limits.</summary>
    private void Sanitize()
    {
        FontSizePt = double.IsFinite(FontSizePt) ? Math.Clamp(FontSizePt, MinFontSizePt, MaxFontSizePt) : Look.DefaultFontSizePt;
        TabWidth = Math.Clamp(TabWidth, 1, 16);
        if (string.IsNullOrWhiteSpace(FontName))
        {
            FontName = Look.DefaultFontName;
        }

        Accent = Palettes.AccentNamed(Accent).Name;
        if (!Enum.IsDefined(Theme))
        {
            Theme = ThemeMode.Dark;
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(SessionState))]
internal sealed partial class SettingsJson : JsonSerializerContext;
