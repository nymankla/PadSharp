using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace PadSharp;

internal sealed class App : Application
{
    private readonly ColorPaletteResources darkFluent = FluentPalette(Palettes.Dark);
    private readonly ColorPaletteResources lightFluent = FluentPalette(Palettes.Light);

    /// <summary>Sets Fluent's accent (toggles, accent buttons, focus rings) for both variants.</summary>
    public void SetAccent(Color color)
    {
        darkFluent.Accent = color;
        lightFluent.Accent = color;
    }

    public override void Initialize()
    {
        // MainWindow picks the real variant and accent from the saved settings.
        RequestedThemeVariant = ThemeVariant.Dark;
        var fluent = new FluentTheme();
        fluent.Palettes[ThemeVariant.Dark] = darkFluent;
        fluent.Palettes[ThemeVariant.Light] = lightFluent;
        Styles.Add(fluent);
        Styles.Add(new EditorStyles());

        // Theme resources the Fluent templates read: tabs blend into the editor when selected.
        Resources["TabItemHeaderBackgroundSelected"] = Look.EditorBack;
        Resources["TabItemHeaderBackgroundSelectedPointerOver"] = Look.EditorBack;
        Resources["TabItemHeaderBackgroundSelectedPressed"] = Look.EditorBack;
        Resources["TabItemHeaderBackgroundUnselected"] = Brushes.Transparent;
        Resources["TabItemHeaderBackgroundUnselectedPointerOver"] = Look.HoverBack;
        Resources["TabItemHeaderBackgroundUnselectedPressed"] = Look.HoverBack;
        Resources["TabItemHeaderForegroundSelected"] = Look.EditorFore;
        Resources["TabItemHeaderForegroundUnselected"] = Look.MutedFore;
        Resources["ButtonBackgroundPointerOver"] = Look.HoverBack;

        Styles.Add(new Style(x => x.OfType<TabItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.FontSizeProperty, 12.5),
                new Setter(Layoutable.MinHeightProperty, 34.0),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 4, 6, 4)),
                new Setter(Layoutable.MarginProperty, new Thickness(2, 0, 0, 0)),
                new Setter(TemplatedControl.CornerRadiusProperty, new CornerRadius(Look.Radius, Look.Radius, 0, 0)),
            },
        });
        Styles.Add(new Style(x => x.OfType<TabItem>().Template().OfType<Border>().Name("PART_SelectedPipe"))
        {
            Setters =
            {
                new Setter(Border.BackgroundProperty, Look.AccentGradient),
                new Setter(Border.CornerRadiusProperty, new CornerRadius(2)),
            },
        });
        Styles.Add(new Style(x => x.OfType<MenuItem>())
        {
            Setters = { new Setter(TemplatedControl.FontSizeProperty, 12.5) },
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string? screenshot = null;
            var files = new List<string>();
            string[] args = desktop.Args ?? [];
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--screenshot" && i + 1 < args.Length)
                {
                    screenshot = Path.GetFullPath(args[++i]);
                }
                else
                {
                    files.Add(args[i]);
                }
            }

            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.MainWindow = new MainWindow(files, screenshot, AppSettings.Load(), code => desktop.Shutdown(code));
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Fluent's own colours (menus, inputs, flyouts) derived from one of our palettes.</summary>
    private static ColorPaletteResources FluentPalette(Palette palette) => new()
    {
        Accent = Palettes.Accents[0].From,
        RegionColor = palette.Panel,
        AltHigh = palette.Window,
        ChromeMediumLow = palette.Raised,
        ChromeMedium = palette.Raised,
        ChromeAltLow = palette.Raised,
    };
}
