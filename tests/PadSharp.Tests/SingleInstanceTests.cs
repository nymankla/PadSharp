using System.IO.Pipes;
using System.Text;
using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class SingleInstanceTests : IDisposable
{
    private readonly Sandbox sandbox = new();
    // Short on purpose: on macOS the socket path (temp folder + "CoreFxPipe_" + name) must stay under 104 characters.
    private readonly string pipe = "PST-" + Guid.NewGuid().ToString("N")[..8];

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void FilesReachTheRunningInstanceAsFullPaths()
    {
        var received = new List<IReadOnlyList<string>>();
        using (new SingleInstanceServer(pipe, received.Add))
        {
            Assert.True(SingleInstance.TrySend(pipe, ["notes.txt", Path.Combine(sandbox.Files, "b.md")], TimeSpan.FromSeconds(5)));
            Assert.True(SingleInstance.TrySend(pipe, [], TimeSpan.FromSeconds(5)));
        }

        Assert.Equal([Path.GetFullPath("notes.txt"), Path.Combine(sandbox.Files, "b.md")], received[0]);
        Assert.Empty(received[1]);
    }

    [AvaloniaFact]
    public void QuickSuccessiveStartsAllGetThrough()
    {
        var received = new List<IReadOnlyList<string>>();
        using (new SingleInstanceServer(pipe, files => { lock (received) { received.Add(files); } }))
        {
            for (int i = 0; i < 10; i++)
            {
                Assert.True(SingleInstance.TrySend(pipe, [$"file{i}.txt"], TimeSpan.FromSeconds(5)), $"send {i}");
            }
        }

        Assert.Equal(10, received.Count);
    }

    [AvaloniaFact]
    public void ThePipeNameFitsAUnixSocketPath() =>
        // macOS limits a socket path to 104 characters, and its temp folder alone can be about 50.
        Assert.True(SingleInstance.PipeName.Length <= 30, SingleInstance.PipeName);

    [AvaloniaFact]
    public void NobodyListeningMeansFalse() =>
        Assert.False(SingleInstance.TrySend(pipe, ["a.txt"], TimeSpan.FromMilliseconds(200)));

    [AvaloniaFact]
    public void AClientThatDoesNotSpeakTheProtocolIsIgnored()
    {
        var received = new List<IReadOnlyList<string>>();
        using var server = new SingleInstanceServer(pipe, received.Add);

        using (var stranger = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.CurrentUserOnly))
        {
            stranger.Connect(5000);
            byte[] junk = Encoding.UTF8.GetBytes("hello?\n/etc/passwd\n\n");
            stranger.Write(junk);
        }

        Assert.True(SingleInstance.TrySend(pipe, ["after.txt"], TimeSpan.FromSeconds(5)));
        Assert.Equal([Path.GetFullPath("after.txt")], Assert.Single(received));
    }

    [AvaloniaFact]
    public void ThePipeBelongsToTheUserAndDataFolder()
    {
        string first = SingleInstance.PipeName;
        Assert.StartsWith("PadSharp-", first, StringComparison.Ordinal);
        Assert.Equal(first, SingleInstance.PipeName);

        AppSettings.DataFolder = Path.Combine(sandbox.Root, "portable");
        Assert.NotEqual(first, SingleInstance.PipeName);
    }
}
