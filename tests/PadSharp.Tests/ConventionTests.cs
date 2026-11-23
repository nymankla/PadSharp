using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

/// <summary>Holds the repository to the rule in CLAUDE.md: every source file has a test class.</summary>
public sealed class ConventionTests
{
    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PadSharp.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("PadSharp.slnx not found above the test output.");
    }

    /// <summary><c>src/MainWindow.Find.cs</c> → <c>MainWindowFindTests</c>; <c>EditorStyles.axaml.cs</c> → <c>EditorStylesTests</c>.</summary>
    internal static string TestClassFor(string fileName) =>
        fileName.Replace(".axaml.cs", string.Empty, StringComparison.Ordinal)
            .Replace(".cs", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal) + "Tests";

    [AvaloniaFact]
    public void EverySourceFileHasATestClass()
    {
        string[] missing = [.. Directory.EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.cs")
            .Select(f => TestClassFor(Path.GetFileName(f)))
            .Where(name => typeof(ConventionTests).Assembly.GetType("PadSharp.Tests." + name) is null)];

        Assert.Empty(missing);
    }

    [AvaloniaFact]
    public void EverySyntaxFileBacksALanguage()
    {
        string[] unused = [.. Directory.EnumerateFiles(Path.Combine(RepoRoot(), "syntax"), "*.xshd")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => !Languages.All.Any(l => l.Highlighting == name))];

        Assert.Empty(unused);
    }

    [AvaloniaFact]
    public void LaunchersStartTheProgramInTheirOwnFolderWhateverTheyAreCalled()
    {
        string dist = Path.Combine(RepoRoot(), "dist");
        string cmd = File.ReadAllText(Path.Combine(dist, "ed.cmd"));
        string sh = File.ReadAllText(Path.Combine(dist, "ed"));

        // %~dp0 and the script's resolved folder: the program is found next to the script, never by its name.
        Assert.Contains("\"%~dp0PadSharp.exe\" %*", cmd, StringComparison.Ordinal);
        Assert.Contains("\"$dir/PadSharp\" \"$@\"", sh, StringComparison.Ordinal);
        Assert.DoesNotContain("%~n0", cmd, StringComparison.Ordinal);
        Assert.DoesNotContain("basename", sh, StringComparison.Ordinal);
        Assert.Contains("Rename this file to anything", cmd, StringComparison.Ordinal);
        Assert.Contains("Rename this file to anything", sh, StringComparison.Ordinal);

        Assert.StartsWith("#!/bin/sh\n", sh, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', sh);
    }

    [AvaloniaFact]
    public void LinuxAndMacFilesUseLfAndKeepTheirPlaceholders()
    {
        string dist = Path.Combine(RepoRoot(), "dist");
        foreach (string file in new[] { "ed", "linux/install.sh", "linux/padsharp.desktop", "linux/padsharp.svg", "macos/Info.plist" })
        {
            Assert.DoesNotContain('\r', File.ReadAllText(Path.Combine(dist, file)));
        }

        Assert.StartsWith("#!/bin/sh\n", File.ReadAllText(Path.Combine(dist, "linux", "install.sh")), StringComparison.Ordinal);
        Assert.Contains("Exec=\"@EXEC@\" %F", File.ReadAllText(Path.Combine(dist, "linux", "padsharp.desktop")), StringComparison.Ordinal);
        string plist = File.ReadAllText(Path.Combine(dist, "macos", "Info.plist"));
        Assert.Contains("<string>@VERSION@</string>", plist, StringComparison.Ordinal);
        Assert.Contains("<string>@BUNDLE_ID@</string>", plist, StringComparison.Ordinal);
        Assert.Contains("<string>PadSharp</string>", plist, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheReleaseWorkflowPackagesEveryTarget()
    {
        string release = File.ReadAllText(Path.Combine(RepoRoot(), ".github", "workflows", "release.yml"));

        foreach (string rid in new[] { "win-x64", "linux-x64", "osx-arm64" })
        {
            Assert.Contains($"rid: {rid}", release, StringComparison.Ordinal);
        }

        Assert.Contains("<Version>${version}</Version>", release, StringComparison.Ordinal);
        Assert.Contains("dist/linux/install.sh", release, StringComparison.Ordinal);
        Assert.Contains("dist/macos/Info.plist", release, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TestClassNamesFollowTheFileNames()
    {
        Assert.Equal("MainWindowFindTests", TestClassFor("MainWindow.Find.cs"));
        Assert.Equal("EditorStylesTests", TestClassFor("EditorStyles.axaml.cs"));
        Assert.Equal("TextFileTests", TestClassFor("TextFile.cs"));
    }
}
