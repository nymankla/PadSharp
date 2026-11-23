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

    /// <summary>Zoom steps (points added to the font size), kept between runs.</summary>
    public int Zoom { get; set; }

    /// <summary>The window's size when last closed (not maximized), in device-independent pixels.</summary>
    public double WindowWidth { get; set; } = 1100;

    public double WindowHeight { get; set; } = 720;

    /// <summary>The window's top-left corner when last closed, in screen pixels; null lets the system place it.</summary>
    public int? WindowX { get; set; }

    public int? WindowY { get; set; }

    public bool WindowMaximized { get; set; }

    /// <summary>
    /// The folder for settings and the session: <c>PADSHARP_DATA</c> when set (a portable copy, or tests),
    /// otherwise e.g. %APPDATA%\PadSharp on Windows.
    /// </summary>
    public static string DataFolder { get; set; } = Environment.GetEnvironmentVariable("PADSHARP_DATA") is { Length: > 0 } custom
        ? Path.GetFullPath(custom)
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadSharp");

    /// <summary>Where settings are stored, e.g. %APPDATA%\PadSharp\settings.json on Windows.</summary>
    public static string FilePath => Path.Combine(DataFolder, "settings.json");

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

    /// <summary>Writes the settings; returns why that failed, or null when it worked.</summary>
    public string? Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, SettingsJson.Default.AppSettings));
            File.Move(temp, FilePath, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    /// <summary>Keeps hand-edited values inside sensible limits.</summary>
    private void Sanitize()
    {
        FontSizePt = double.IsFinite(FontSizePt) ? Math.Clamp(FontSizePt, MinFontSizePt, MaxFontSizePt) : Look.DefaultFontSizePt;
        TabWidth = Math.Clamp(TabWidth, 1, 16);
        Zoom = Math.Clamp(Zoom, Look.MinZoom, Look.MaxZoom);
        WindowWidth = double.IsFinite(WindowWidth) ? Math.Clamp(WindowWidth, 400, 20000) : 1100;
        WindowHeight = double.IsFinite(WindowHeight) ? Math.Clamp(WindowHeight, 300, 20000) : 720;
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
