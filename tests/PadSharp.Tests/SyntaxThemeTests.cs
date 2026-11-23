using Avalonia.Headless.XUnit;
using Avalonia.Media;
using AvaloniaEdit.Highlighting;

namespace PadSharp.Tests;

public sealed class SyntaxThemeTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    private static IHighlightingDefinition Definition(string name)
    {
        SyntaxTheme.EnsureRegistered();
        return HighlightingManager.Instance.GetDefinition(name);
    }

    private static Color? Foreground(string definition, string colour) =>
        Definition(definition).GetNamedColor(colour)?.Foreground?.GetColor(null!);

    [AvaloniaFact]
    public void EveryRegisteredDefinitionLoads()
    {
        // Definitions load lazily, so only touching MainRuleSet proves the .xshd parses.
        string[] broken = [.. Languages.All.Select(l => l.Highlighting).OfType<string>().Distinct()
            .Where(name => Record.Exception(() => Definition(name).MainRuleSet) is not null)];

        Assert.Empty(broken);
    }

    [AvaloniaFact]
    public void ColoursFollowTheirNamesRoles()
    {
        SyntaxPalette syntax = Look.Syntax;

        Assert.Equal(syntax.Comment, Foreground("Rust", "Comment"));
        Assert.Equal(syntax.Keyword, Foreground("Rust", "Keyword"));
        Assert.Equal(syntax.Type, Foreground("Rust", "BuiltinType"));
        Assert.Equal(syntax.Function, Foreground("Rust", "MacroCall"));
        Assert.Equal(syntax.Variable, Foreground("Rust", "LifetimeParam"));
        Assert.Equal(syntax.Attribute, Foreground("Rust", "Attribute"));
        Assert.Equal(syntax.String, Foreground("Rust", "Char"));
        Assert.Equal(syntax.Type, Foreground("INI", "Section"));
        Assert.Equal(syntax.Operator, Foreground("Batch", "Hide"));
    }

    [AvaloniaFact]
    public void BackgroundsAndBoldAreDroppedAndCommentsAreItalic()
    {
        HighlightingColor label = Definition("Batch").GetNamedColor("Label");
        HighlightingColor comment = Definition("Batch").GetNamedColor("Comment");

        Assert.Null(label.Background);
        Assert.Null(label.FontWeight);
        Assert.Equal(FontStyle.Italic, comment.FontStyle);
    }

    [AvaloniaFact]
    public void ResetRebuildsWithTheNewPalette()
    {
        _ = Definition("Rust");
        Look.Apply(Palettes.Light, Palettes.Accents[0]);
        SyntaxTheme.Reset();

        Assert.Equal(Palettes.Light.Syntax.Comment, Foreground("Rust", "Comment"));
    }
}
