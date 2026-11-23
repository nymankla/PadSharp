using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Path = Avalonia.Controls.Shapes.Path;

namespace PadSharp;

/// <summary>Small line-drawn toolbar icons (16 x 16), the app logo and the window icon, drawn in code.</summary>
internal static class Icons
{
    public const string New = "M3,1.5 H10 L13,4.5 V14.5 H3 Z M10,1.5 V4.5 H13";
    public const string Open = "M1.5,4 H6 L7.5,5.5 H14.5 V13.5 H1.5 Z";
    public const string Save = "M2,2 H12 L14,4 V14 H2 Z M5,2 V6 H11 V2 M4.5,14 V9.5 H11.5 V14";
    public const string SaveAll = "M4,4 H12 L14,6 V15 H4 Z M6.5,4 V7 H11 V4 M2,12 V1.5 H10";
    public const string Close = "M3.5,3.5 L12.5,12.5 M12.5,3.5 L3.5,12.5";
    public const string Cut = "M5,1 L11,10.5 M11,1 L5,10.5 M2,12.5 A2,2 0 1 0 6,12.5 A2,2 0 1 0 2,12.5 M10,12.5 A2,2 0 1 0 14,12.5 A2,2 0 1 0 10,12.5";
    public const string Copy = "M5,5 H14 V15 H5 Z M2,11 V1.5 H11 V4";
    public const string Paste = "M4,3 H2.5 V15 H13.5 V3 H12 M5.5,1.5 H10.5 V4.5 H5.5 Z M5,8 H11 M5,11 H11";
    public const string Undo = "M5,3 L1.5,6.5 L5,10 M1.5,6.5 H10 A4,4 0 0 1 10,14.5 H6";
    public const string Redo = "M11,3 L14.5,6.5 L11,10 M14.5,6.5 H6 A4,4 0 0 0 6,14.5 H10";
    public const string Find = "M6.5,1.5 A5,5 0 1 0 6.5,11.5 A5,5 0 1 0 6.5,1.5 M10,10 L14.5,14.5";
    public const string Replace = "M2,4 H12 M9.5,1.5 L12,4 L9.5,6.5 M14,12 H4 M6.5,9.5 L4,12 L6.5,14.5";
    public const string ZoomIn = Find + " M4,6.5 H9 M6.5,4 V9";
    public const string ZoomOut = Find + " M4,6.5 H9";
    public const string Sun = "M8,5 A3,3 0 1 0 8,11 A3,3 0 1 0 8,5 M8,1 V2.5 M8,13.5 V15 M1,8 H2.5 M13.5,8 H15 M3,3 L4,4 M12,12 L13,13 M3,13 L4,12 M12,4 L13,3";
    public const string Moon = "M13.5,10 A6,6 0 1 1 6,2.5 A4.8,4.8 0 0 0 13.5,10 Z";
    public const string Sliders = "M2,4 H14 M2,8 H14 M2,12 H14 M5,2.5 V5.5 M10.5,6.5 V9.5 M7,10.5 V13.5";
    public const string Wrap ="M1.5,3 H14.5 M1.5,8 H12 A2.5,2.5 0 0 1 12,13 H8 M10,11 L8,13 L10,15 M1.5,13 H5";

    public static Path Make(string data) => new()
    {
        Data = Geometry.Parse(data),
        Stroke = Look.IconStroke,
        StrokeThickness = 1.4,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        Width = 16,
        Height = 16,
    };

    /// <summary>The logo: a gradient tile with three text lines of falling length and a caret.</summary>
    public static Canvas Logo(double size)
    {
        double s = size / 32;
        var canvas = new Canvas { Width = size, Height = size };
        canvas.Children.Add(new Rectangle
        {
            Width = size,
            Height = size,
            RadiusX = 8 * s,
            RadiusY = 8 * s,
            Fill = Look.LogoGradient,
        });
        canvas.Children.Add(LogoStroke("M8,10 H24 M8,16 H20 M8,22 H14", Brushes.White, 3, s));
        canvas.Children.Add(LogoStroke("M19,19 V25", new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)), 2.5, s));
        return canvas;
    }

    /// <summary>A rounded stroke drawn on the logo's 32-unit grid, scaled to its size.</summary>
    private static Path LogoStroke(string data, IBrush stroke, double thickness, double scale) => new()
    {
        Data = Geometry.Parse(data),
        Stroke = stroke,
        StrokeThickness = thickness,
        StrokeLineCap = PenLineCap.Round,
        RenderTransform = new ScaleTransform(scale, scale),
        RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute),
    };

    /// <summary>A round sample of an accent's gradient.</summary>
    public static Ellipse Swatch(Accent accent, double size)
    {
        ArgumentNullException.ThrowIfNull(accent);
        return new Ellipse
        {
            Width = size,
            Height = size,
            Fill = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(accent.From, 0), new GradientStop(accent.To, 1) },
            },
        };
    }

    public static WindowIcon CreateWindowIcon()
    {
        Canvas canvas = Logo(32);
        canvas.Measure(new Size(32, 32));
        canvas.Arrange(new Rect(0, 0, 32, 32));
        var bitmap = new RenderTargetBitmap(new PixelSize(32, 32));
        bitmap.Render(canvas);
        return new WindowIcon(bitmap);
    }
}
