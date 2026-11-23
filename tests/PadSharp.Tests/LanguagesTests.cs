using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class LanguagesTests
{
    [AvaloniaTheory]
    [InlineData("x.cs", "C#")]
    [InlineData(@"C:\src\MAIN.RS", "Rust")]
    [InlineData(".editorconfig", "INI")]
    [InlineData("notes.txt", "Plain Text")]
    [InlineData("a.yaml", "YAML")]
    public void FromExtensionPicksTheLanguage(string path, string expected) =>
        Assert.Equal(expected, Languages.FromExtension(path)?.Name);

    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Makefile")]
    [InlineData("data.unknownext")]
    public void FromExtensionIsNullWhenNoLanguageClaimsIt(string? path) => Assert.Null(Languages.FromExtension(path));

    [AvaloniaTheory]
    [InlineData("#!/usr/bin/env python3\nprint()", "Python")]
    [InlineData("#!/usr/bin/env pwsh", "PowerShell")]
    [InlineData("#!/bin/bash", "Shell")]
    [InlineData("#!/bin/zsh", "Shell")]
    [InlineData("#!/usr/bin/env node", "JavaScript")]
    [InlineData("#!/usr/bin/env rust-script", "Rust")]
    [InlineData("#!/usr/bin/php", "PHP")]
    [InlineData("<?xml version=\"1.0\"?>", "XML")]
    [InlineData("\uFEFF  <?xml version=\"1.0\"?>", "XML")]
    [InlineData("<!DOCTYPE html>\r\n<html>", "HTML")]
    [InlineData("<HTML>", "HTML")]
    [InlineData("<?php echo 1;", "PHP")]
    public void FromContentReadsTheFirstLine(string text, string expected) =>
        Assert.Equal(expected, Languages.FromContent(text)?.Name);

    [AvaloniaTheory]
    [InlineData("#!/usr/bin/perl")]
    [InlineData("hello\n<?xml")]
    [InlineData("")]
    public void FromContentIsNullOtherwise(string text) => Assert.Null(Languages.FromContent(text));

    [AvaloniaFact]
    public void DetectPrefersTheExtensionThenTheContentThenPlainText()
    {
        Assert.Equal("C#", Languages.Detect("a.cs", "#!/bin/bash").Name);
        Assert.Equal("Shell", Languages.Detect("script", "#!/bin/bash").Name);
        Assert.Same(Languages.NormalText, Languages.Detect(null, "text"));
    }

    [AvaloniaFact]
    public void TheCatalogIsSortedWithUniqueNamesAndExtensions()
    {
        IReadOnlyList<Language> all = Languages.All;
        Assert.Equal(all.Select(l => l.Name).Order(StringComparer.OrdinalIgnoreCase), all.Select(l => l.Name));
        Assert.Equal(all.Count, all.Select(l => l.Name).Distinct().Count());

        string[] extensions = [.. all.SelectMany(l => l.Extensions)];
        Assert.Equal(extensions.Length, extensions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(Languages.NormalText, all);
    }

    [AvaloniaFact]
    public void PatternsAreFilePickerGlobs() =>
        Assert.Equal(["*.yml", "*.yaml"], Languages.All.Single(l => l.Name == "YAML").Patterns);

    [AvaloniaFact]
    public void EveryColouredLanguageHasADefinition()
    {
        Assert.Null(Languages.DefinitionFor(Languages.NormalText));
        foreach (Language language in Languages.All.Where(l => l.Highlighting is not null))
        {
            Assert.True(Languages.DefinitionFor(language) is not null, $"{language.Name} has no definition");
        }
    }
}
