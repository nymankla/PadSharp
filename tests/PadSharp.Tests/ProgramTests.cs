using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class ProgramTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

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
