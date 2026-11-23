using Avalonia;
using Avalonia.Media;

namespace PadSharp;

/// <summary>Which palette to use; System follows the operating system's light/dark setting.</summary>
internal enum ThemeMode
{
    System,
    Dark,
    Light,
}

/// <summary>Colours for syntax highlighting, by role.</summary>
internal sealed record SyntaxPalette(
    Color Comment, Color Keyword, Color Type, Color String, Color Number, Color Function, Color Attribute,
    Color Operator, Color Preprocessor, Color Variable, Color Tag, Color Heading);

/// <summary>The neutral colours of one theme: surfaces, text and syntax. The accent comes separately.</summary>
internal sealed record Palette(
    bool IsDark, Color Window, Color Panel, Color Editor, Color Raised, Color Hover, Color Line,
    Color Fore, Color Muted, Color GutterFore, Color CurrentLine,
    Color Icon, Color Saved, Color Unsaved, Color Problem, Color Info, SyntaxPalette Syntax);

/// <summary>
/// An accent colour variant: a gradient pair for small highlights and a deeper pair for the status bar,
/// dark enough for white text.
/// </summary>
internal sealed record Accent(string Name, Color From, Color To, Color StatusFrom, Color StatusTo);

/// <summary>The built-in themes and accent variants.</summary>
internal static class Palettes
{
    public static readonly Palette Dark = new(
        IsDark: true,
        Window: Rgb(0x0F1017), Panel: Rgb(0x161722), Editor: Rgb(0x1A1B26), Raised: Rgb(0x24263A),
        Hover: Rgb(0x2A2C44), Line: Rgb(0x26283A),
        Fore: Rgb(0xE6E8F2), Muted: Rgb(0x8B90A8), GutterFore: Rgb(0x4A4E68),
        CurrentLine: Color.FromArgb(0x60, 0x2A, 0x2C, 0x48),
        Icon: Rgb(0xC9CCE0), Saved: Rgb(0x34D399), Unsaved: Rgb(0xFBBF24),
        Problem: Rgb(0xFB7185), Info: Rgb(0x7DD3FC),
        Syntax: new SyntaxPalette(
            Comment: Rgb(0x636A8A), Keyword: Rgb(0xC084FC), Type: Rgb(0x5EEAD4), String: Rgb(0xA3E635),
            Number: Rgb(0xFDBA74), Function: Rgb(0x60A5FA), Attribute: Rgb(0xFDE047), Operator: Rgb(0xF472B6),
            Preprocessor: Rgb(0xFB923C), Variable: Rgb(0xF9A8D4), Tag: Rgb(0xFB7185), Heading: Rgb(0xE879F9)));

    public static readonly Palette Light = new(
        IsDark: false,
        Window: Rgb(0xF1F0F7), Panel: Rgb(0xF8F7FC), Editor: Rgb(0xFFFFFF), Raised: Rgb(0xFFFFFF),
        Hover: Rgb(0xE5E1F8), Line: Rgb(0xDFDDEA),
        Fore: Rgb(0x1F2133), Muted: Rgb(0x6B7085), GutterFore: Rgb(0xB0B3C6),
        CurrentLine: Rgb(0xF5F2FF),
        Icon: Rgb(0x3B3F55), Saved: Rgb(0x10B981), Unsaved: Rgb(0xF59E0B),
        Problem: Rgb(0xE11D48), Info: Rgb(0x0284C7),
        Syntax: new SyntaxPalette(
            Comment: Rgb(0x8A8FA8), Keyword: Rgb(0x7C3AED), Type: Rgb(0x0D9488), String: Rgb(0x16A34A),
            Number: Rgb(0xEA580C), Function: Rgb(0x2563EB), Attribute: Rgb(0xB45309), Operator: Rgb(0xDB2777),
            Preprocessor: Rgb(0xC2410C), Variable: Rgb(0xBE185D), Tag: Rgb(0xE11D48), Heading: Rgb(0xA21CAF)));

    /// <summary>Accent variants; the first is the default.</summary>
    public static IReadOnlyList<Accent> Accents { get; } =
    [
        new("Neon", Rgb(0x8B5CF6), Rgb(0xEC4899), Rgb(0x6D3FE0), Rgb(0xC0347F)),
        new("Ocean", Rgb(0x3B82F6), Rgb(0x06B6D4), Rgb(0x1D4ED8), Rgb(0x0E7490)),
        new("Aurora", Rgb(0x10B981), Rgb(0x8B5CF6), Rgb(0x047857), Rgb(0x6D28D9)),
        new("Sunset", Rgb(0xF97316), Rgb(0xEC4899), Rgb(0xC2410C), Rgb(0xBE185D)),
        new("Ember", Rgb(0xEF4444), Rgb(0xF59E0B), Rgb(0xB91C1C), Rgb(0xB45309)),
        new("Forest", Rgb(0x22C55E), Rgb(0x14B8A6), Rgb(0x15803D), Rgb(0x0F766E)),
        new("Mono", Rgb(0x64748B), Rgb(0x94A3B8), Rgb(0x334155), Rgb(0x475569)),
    ];

    /// <summary>The accent with this name, or the default.</summary>
    public static Accent AccentNamed(string? name) =>
        Accents.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Accents[0];

    internal static Color Rgb(uint rgb) => Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
}

/// <summary>
/// Colours and fonts. The brushes are shared and recoloured in place by <see cref="Apply"/>, so switching theme
/// or accent updates every control that uses them without rebuilding the window.
/// </summary>
internal static class Look
{
    public const double DefaultFontSizePt = 10.5;
    public const string DefaultFontName = "Cascadia Code";
    public const int MinZoom = -10;
    public const int MaxZoom = 60;
    public const int DefaultTabWidth = 4;
    public const double Radius = 8;

    public static readonly SolidColorBrush ChromeBack = new(Palettes.Dark.Window);
    public static readonly SolidColorBrush PanelBack = new(Palettes.Dark.Panel);
    public static readonly SolidColorBrush EditorBack = new(Palettes.Dark.Editor);
    public static readonly SolidColorBrush RaisedBack = new(Palettes.Dark.Raised);
    public static readonly SolidColorBrush HoverBack = new(Palettes.Dark.Hover);
    public static readonly SolidColorBrush ChromeLine = new(Palettes.Dark.Line);
    public static readonly SolidColorBrush EditorFore = new(Palettes.Dark.Fore);
    public static readonly SolidColorBrush MutedFore = new(Palettes.Dark.Muted);
    public static readonly SolidColorBrush GutterBack = EditorBack;
    public static readonly SolidColorBrush GutterFore = new(Palettes.Dark.GutterFore);
    public static readonly SolidColorBrush GutterActiveFore = new(Palettes.Accents[0].To);
    public static readonly SolidColorBrush CurrentLineBack = new(Palettes.Dark.CurrentLine);
    public static readonly SolidColorBrush SelectionBack = new(Palettes.Accents[0].From);
    public static readonly SolidColorBrush Caret = new(Palettes.Accents[0].To);
    public static readonly SolidColorBrush IconStroke = new(Palettes.Dark.Icon);
    public static readonly SolidColorBrush SavedDot = new(Palettes.Dark.Saved);
    public static readonly SolidColorBrush UnsavedDot = new(Palettes.Dark.Unsaved);
    public static readonly SolidColorBrush ProblemText = new(Palettes.Dark.Problem);
    public static readonly SolidColorBrush InfoText = new(Palettes.Dark.Info);

    /// <summary>The accent gradient, left to right.</summary>
    public static readonly LinearGradientBrush AccentGradient = Gradient(new RelativePoint(0, 0.5, RelativeUnit.Relative), new RelativePoint(1, 0.5, RelativeUnit.Relative));

    /// <summary>The accent gradient, corner to corner, for the logo tile.</summary>
    public static readonly LinearGradientBrush LogoGradient = Gradient(new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(1, 1, RelativeUnit.Relative));

    /// <summary>The deeper accent gradient used for the status bar.</summary>
    public static readonly LinearGradientBrush StatusGradient = Gradient(new RelativePoint(0, 0.5, RelativeUnit.Relative), new RelativePoint(1, 0.5, RelativeUnit.Relative));

    /// <summary>The palette in use.</summary>
    public static Palette Current { get; private set; } = Palettes.Dark;

    /// <summary>The accent in use.</summary>
    public static Accent CurrentAccent { get; private set; } = Palettes.Accents[0];

    /// <summary>Syntax colours of the palette in use.</summary>
    public static SyntaxPalette Syntax => Current.Syntax;

    /// <summary>The chosen font first, then modern coding fonts, then the classics on each system.</summary>
    public static FontFamily EditorFontFor(string name) =>
        new($"{name}, Cascadia Code, JetBrains Mono, Consolas, Menlo, DejaVu Sans Mono, Courier New");

    /// <summary>Recolours every shared brush to the given palette and accent.</summary>
    public static void Apply(Palette palette, Accent accent)
    {
        ArgumentNullException.ThrowIfNull(palette);
        ArgumentNullException.ThrowIfNull(accent);
        Current = palette;
        CurrentAccent = accent;
        ChromeBack.Color = palette.Window;
        PanelBack.Color = palette.Panel;
        EditorBack.Color = palette.Editor;
        RaisedBack.Color = palette.Raised;
        HoverBack.Color = palette.Hover;
        ChromeLine.Color = palette.Line;
        EditorFore.Color = palette.Fore;
        MutedFore.Color = palette.Muted;
        GutterFore.Color = palette.GutterFore;
        CurrentLineBack.Color = palette.CurrentLine;
        IconStroke.Color = palette.Icon;
        SavedDot.Color = palette.Saved;
        UnsavedDot.Color = palette.Unsaved;
        ProblemText.Color = palette.Problem;
        InfoText.Color = palette.Info;

        GutterActiveFore.Color = palette.IsDark ? Mix(accent.To, Colors.White, 0.35) : accent.StatusTo;
        Caret.Color = accent.To;
        SelectionBack.Color = Color.FromArgb(palette.IsDark ? (byte)0x90 : (byte)0x50, accent.From.R, accent.From.G, accent.From.B);
        SetStops(AccentGradient, accent.From, accent.To);
        SetStops(LogoGradient, accent.From, accent.To);
        SetStops(StatusGradient, accent.StatusFrom, accent.StatusTo);
    }

    /// <summary>Point size for a base size and zoom level: each step adds one point, never below 2.</summary>
    public static double FontSizeForZoomPt(double basePt, int zoom) => Math.Max(2, basePt + zoom);

    /// <summary>Converts points to device-independent pixels (1/96 inch).</summary>
    public static double PtToDip(double pt) => pt * 96.0 / 72.0;

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + ((b.R - a.R) * t)), (byte)(a.G + ((b.G - a.G) * t)), (byte)(a.B + ((b.B - a.B) * t)));

    private static void SetStops(LinearGradientBrush brush, Color from, Color to)
    {
        brush.GradientStops[0].Color = from;
        brush.GradientStops[1].Color = to;
    }

    private static LinearGradientBrush Gradient(RelativePoint start, RelativePoint end) => new()
    {
        StartPoint = start,
        EndPoint = end,
        GradientStops = { new GradientStop(Palettes.Accents[0].From, 0), new GradientStop(Palettes.Accents[0].To, 1) },
    };
}
