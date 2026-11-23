using AvaloniaEdit.Highlighting;

namespace PadSharp;

/// <summary>
/// A language the editor knows: its name (menu and status bar), the file extensions that select it,
/// and the name of the highlighting definition used to colour it.
/// </summary>
internal sealed record Language(string Name, IReadOnlyList<string> Extensions, string? Highlighting)
{
    /// <summary>File-picker pattern list such as <c>*.cs</c>.</summary>
    public IReadOnlyList<string> Patterns => [.. Extensions.Select(e => "*." + e)];
}

/// <summary>The language catalog, detection rules and highlighting lookup.</summary>
internal static class Languages
{
    public static readonly Language NormalText = new("Plain Text", ["txt"], null);

    /// <summary>Every language, sorted by menu name.</summary>
    public static IReadOnlyList<Language> All { get; } =
    [
        .. new Language[]
        {
            NormalText,
            new("ASP", ["asp", "aspx", "ascx", "asmx", "ashx"], "ASP/XHTML"),
            new("Batch", ["bat", "cmd", "nt"], "Batch"),
            new("C", ["c", "lex"], "C++"),
            new("C#", ["cs"], "C#"),
            new("C++", ["cpp", "cxx", "cc", "h", "hh", "hpp", "hxx", "ino"], "C++"),
            new("CSS", ["css"], "CSS"),
            new("Diff", ["diff", "patch"], "Patch"),
            new("HTML", ["html", "htm", "shtml", "shtm", "xhtml", "xht", "hta"], "HTML"),
            new("INI", ["ini", "inf", "url", "wer", "editorconfig"], "INI"),
            new("Java", ["java"], "Java"),
            new("JavaScript", ["js", "jsm", "jsx", "mjs", "cjs"], "JavaScript"),
            new("JSON", ["json"], "Json"),
            new("Markdown", ["md", "markdown"], "MarkDown"),
            new("PHP", ["php", "php3", "php4", "php5", "phps", "phpt", "phtml"], "PHP"),
            new("PowerShell", ["ps1", "psm1", "psd1"], "PowerShell"),
            new("Python", ["py", "pyw", "pyi"], "Python"),
            new("Rust", ["rs"], "Rust"),
            new("Shell", ["sh", "bsh", "bash", "zsh"], "Shell"),
            new("SQL", ["sql"], "TSQL"),
            new("TeX", ["tex", "sty"], "TeX"),
            new("TypeScript", ["ts", "tsx", "mts", "cts"], "JavaScript"),
            new("Visual Basic", ["vb", "vba", "vbs"], "VB"),
            new("XML",
                ["xml", "xaml", "axaml", "xsl", "xslt", "xsd", "xul", "kml", "svg", "wsdl", "xlf", "xliff", "gpx", "plist",
                 "csproj", "vbproj", "vcxproj", "props", "targets", "resx", "nuspec", "config", "xshd", "manifest"], "XML"),
            new("YAML", ["yml", "yaml"], "YAML"),
        }.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase),
    ];

    /// <summary>The language a file extension selects, or null when no language claims it.</summary>
    public static Language? FromExtension(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // ".editorconfig" has no name part, so GetExtension returns the whole name: that is what we want.
        string ext = Path.GetExtension(path).TrimStart('.');
        return ext.Length == 0
            ? null
            : All.FirstOrDefault(l => l.Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// <c>#!</c> lines: the first interpreter name the line contains wins, so "sh" comes last
    /// (it is also inside "pwsh", "bash" and "zsh").
    /// </summary>
    private static readonly (string Interpreter, string Language)[] Interpreters =
    [
        ("python", "Python"), ("rust-script", "Rust"), ("pwsh", "PowerShell"), ("node", "JavaScript"), ("php", "PHP"), ("sh", "Shell"),
    ];

    /// <summary>Other first lines, matched by how they start.</summary>
    private static readonly (string Opening, string Language)[] Openings =
    [
        ("<?xml", "XML"), ("<!DOCTYPE html", "HTML"), ("<html", "HTML"), ("<?php", "PHP"),
    ];

    /// <summary>Guesses from how the text begins: a <c>#!</c> line, an XML declaration or an HTML doctype.</summary>
    public static Language? FromContent(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        int end = text.AsSpan().IndexOfAny('\r', '\n');
        string first = (end < 0 ? text : text[..end]).TrimStart('﻿').Trim();
        string? name = first.StartsWith("#!", StringComparison.Ordinal)
            ? Interpreters.FirstOrDefault(e => first.Contains(e.Interpreter, StringComparison.OrdinalIgnoreCase)).Language
            : Openings.FirstOrDefault(e => first.StartsWith(e.Opening, StringComparison.OrdinalIgnoreCase)).Language;
        return name is null ? null : ByName(name);
    }

    /// <summary>Extension first, then the first line, then Plain Text.</summary>
    public static Language Detect(string? path, string text) => FromExtension(path) ?? FromContent(text) ?? NormalText;

    /// <summary>The colouring for a language, or null for plain text.</summary>
    public static IHighlightingDefinition? DefinitionFor(Language language)
    {
        ArgumentNullException.ThrowIfNull(language);
        if (language.Highlighting is null)
        {
            return null;
        }

        SyntaxTheme.EnsureRegistered();
        return HighlightingManager.Instance.GetDefinition(language.Highlighting);
    }

    private static Language ByName(string name) => All.First(l => l.Name == name);
}
