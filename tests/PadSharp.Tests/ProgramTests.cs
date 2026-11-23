using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class ProgramTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    private string In(string name) => Path.Combine(sandbox.Files, name);

    [AvaloniaFact]
    public void TheAppBuilderStartsTheEditorApp() =>
        Assert.Equal(typeof(App), Program.BuildAvaloniaApp().ApplicationType);

    [AvaloniaFact]
    public void TheFirstStartClaimsTheSessionAndRuns()
    {
        Assert.False(Program.ForwardToRunningInstance(["a.txt"]));

        Assert.True(Session.TryTakeOwnership());
    }

    [AvaloniaFact]
    public void ScreenshotRunsNeverForwardOrClaim()
    {
        Assert.False(Program.ForwardToRunningInstance(["a.txt", "--screenshot", "out.png"]));

        Assert.True(Session.TryClaim());
    }

    [AvaloniaFact]
    public void WildcardsInAFileNameBecomeTheMatchingFilesSorted()
    {
        sandbox.File("b.cs", "");
        sandbox.File("A.cs", "");
        sandbox.File("a.csx", "");
        sandbox.File("note1.md", "");
        sandbox.File("note22.md", "");

        Assert.Equal(
            [In("A.cs"), In("b.cs"), In("note1.md"), "plain.txt"],
            Program.ExpandWildcards([Path.Combine(sandbox.Files, "*.cs"), Path.Combine(sandbox.Files, "note?.md"), "plain.txt"]));
    }

    [AvaloniaFact]
    public void APatternWithoutMatchesStaysAsWritten()
    {
        string pattern = Path.Combine(sandbox.Files, "*.nothing");
        string badFolder = Path.Combine(sandbox.Files, "missing", "*.txt");

        Assert.Equal([pattern, badFolder], Program.ExpandWildcards([pattern, badFolder]));
    }

    [AvaloniaFact]
    public void RelativePatternsMatchInTheCurrentFolder()
    {
        string before = Environment.CurrentDirectory;
        sandbox.File("here.txt", "");
        try
        {
            Environment.CurrentDirectory = sandbox.Files;

            Assert.Equal([Path.Combine(".", "here.txt")], Program.ExpandWildcards(["*.txt"]));
        }
        finally
        {
            Environment.CurrentDirectory = before;
        }
    }

    [AvaloniaFact]
    public void TheScreenshotPathIsNeverExpanded()
    {
        sandbox.File("shot.png", "");
        string pattern = Path.Combine(sandbox.Files, "*.png");

        Assert.Equal(["--screenshot", pattern], Program.ExpandWildcards(["--screenshot", pattern]));
    }

    [AvaloniaFact]
    public void ALaterStartHandsItsFilesToTheRunningWindow()
    {
        MainWindow running = sandbox.Open();
        string path = sandbox.File("from-terminal.txt", "hello");

        Assert.True(Program.ForwardToRunningInstance([path]));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("from-terminal.txt", running.Current?.Title);
        Assert.Equal(["from-terminal.txt"], running.Documents.Select(d => d.Title));
    }
}
