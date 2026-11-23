using System.Reflection;
using System.Xml;
using Avalonia.Media;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace PadSharp;

/// <summary>
/// Re-registers every highlighting definition with the app's syntax palette. Definitions name their
/// colours ("Comment", "MethodCall", "AttributeValue"...); each name is mapped to a role in <see cref="Look.Syntax"/>.
/// </summary>
internal static class SyntaxTheme
{
    private const string BuiltInPrefix = "AvaloniaEdit.Highlighting.Resources.";

    private static readonly (string Name, string Resource)[] BuiltIn =
    [
        ("ASP/XHTML", "ASPX.xshd"), ("Boo", "Boo.xshd"), ("Coco", "Coco-Mode.xshd"), ("C++", "CPP-Mode.xshd"),
        ("C#", "CSharp-Mode.xshd"), ("CSS", "CSS-Mode.xshd"), ("HTML", "HTML-Mode.xshd"), ("Java", "Java-Mode.xshd"),
        ("JavaScript", "JavaScript-Mode.xshd"), ("Json", "Json.xshd"), ("MarkDown", "MarkDown-Mode.xshd"),
        ("Patch", "Patch-Mode.xshd"), ("PHP", "PHP-Mode.xshd"), ("PowerShell", "PowerShell.xshd"),
        ("Python", "Python-Mode.xshd"), ("TSQL", "TSQL-Mode.xshd"), ("VB", "VB-Mode.xshd"),
        ("XML", "XML-Mode.xshd"), ("XmlDoc", "XmlDoc.xshd"),
    ];

    private static readonly string[] Custom = ["Batch", "INI", "Rust", "Shell", "TeX", "YAML"];

    private static readonly XmlReaderSettings Safe = new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
    private static bool registered;

    /// <summary>Forgets the coloured definitions so the next lookup builds them with the current palette.</summary>
    public static void Reset() => registered = false;

    /// <summary>Name fragments and the colour they get, checked in order; the first match wins.</summary>
    private static (string[] Fragments, Color Color)[] Roles(SyntaxPalette p) =>
    [
        (["comment", "blockquote", "unchanged"], p.Comment),
        (["number", "digit", "entity", "math"], p.Number),
        (["string", "char", "cdata", "attributevalue", "code", "backtick", "added"], p.String),
        (["preprocessor", "directive", "doctype", "declaration", "document", "pragma", "position"], p.Preprocessor),
        (["type", "class", "section"], p.Type),
        (["keyword", "modifier", "visibility", "truefalse", "command"], p.Keyword),
        (["tag", "selector", "removed"], p.Tag),
        (["attribute", "property", "key", "identifier"], p.Attribute),
        (["method", "function", "label", "call", "link", "image", "url", "filename", "builtin"], p.Function),
        (["heading", "header", "title"], p.Heading),
        (["variable", "reference", "scalar", "param", "emphasis", "this"], p.Variable),
        (["value"], p.String),
        (["operator", "punctuation", "assignment", "hide", "brace", "bracket", "colon", "symbol", "delimiter"], p.Operator),
    ];

    /// <summary>Registers the definitions coloured with the current palette, before any is first used.</summary>
    public static void EnsureRegistered()
    {
        if (registered)
        {
            return;
        }

        registered = true;
        HighlightingManager manager = HighlightingManager.Instance;
        Assembly editor = typeof(HighlightingManager).Assembly;
        foreach ((string name, string resource) in BuiltIn)
        {
            manager.RegisterHighlighting(name, [], () => Load(editor, BuiltInPrefix + resource));
        }

        Assembly app = typeof(SyntaxTheme).Assembly;
        foreach (string name in Custom)
        {
            manager.RegisterHighlighting(name, [], () => Load(app, $"PadSharp.Syntax.{name}.xshd"));
        }
    }

    private static IHighlightingDefinition Load(Assembly assembly, string resource)
    {
        using Stream stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing highlighting resource {resource}.");
        using var reader = XmlReader.Create(stream, Safe);
        XshdSyntaxDefinition xshd = HighlightingLoader.LoadXshd(reader);
        foreach (XshdElement element in xshd.Elements)
        {
            Visit(element);
        }

        return HighlightingLoader.Load(xshd, HighlightingManager.Instance);
    }

    private static void Visit(XshdElement element)
    {
        switch (element)
        {
            case XshdColor color:
                Restyle(color, color.Name, Look.Syntax.Keyword);
                break;
            case XshdRuleSet ruleSet:
                ruleSet.Elements.ToList().ForEach(Visit);
                break;
            case XshdSpan span:
                VisitSpan(span);
                break;
            case XshdKeywords keywords:
                Inline(keywords.ColorReference, Look.Syntax.Keyword);
                break;
            case XshdRule rule:
                Inline(rule.ColorReference, Look.Syntax.Operator);
                break;
            default:
                break;
        }
    }

    /// <summary>A span's own colour defaults to the string role, its begin and end markers to the operator role.</summary>
    private static void VisitSpan(XshdSpan span)
    {
        Inline(span.SpanColorReference, Look.Syntax.String);
        Inline(span.BeginColorReference, Look.Syntax.Operator);
        Inline(span.EndColorReference, Look.Syntax.Operator);
        if (span.RuleSetReference.InlineElement is { } inner)
        {
            Visit(inner);
        }
    }

    private static void Inline(XshdReference<XshdColor> reference, Color fallback)
    {
        if (reference.InlineElement is { } color)
        {
            Restyle(color, color.Name, fallback);
        }
    }

    /// <summary>Gives a colour its role's foreground; drops backgrounds and bold, keeps italics for comments only.</summary>
    private static void Restyle(XshdColor color, string? name, Color fallback)
    {
        if (color.Foreground is null && color.Background is null && color.FontWeight is null)
        {
            return;
        }

        Color role = RoleOf(name) ?? fallback;
        color.Foreground = new SimpleHighlightingBrush(role);
        color.Background = null;
        color.FontWeight = role == Look.Syntax.Heading ? FontWeight.Bold : null;
        color.FontStyle = role == Look.Syntax.Comment ? FontStyle.Italic : null;
    }

    private static Color? RoleOf(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach ((string[] fragments, Color color) in Roles(Look.Syntax))
        {
            if (fragments.Any(f => name.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                return color;
            }
        }

        return null;
    }
}
