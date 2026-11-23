using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;

namespace PadSharp;

/// <summary>
/// One PadSharp per user and data folder. A second start (e.g. <c>ed notes.txt</c> while PadSharp runs) hands its
/// files to the running window over a named pipe that only the current user can open, and exits. The window that
/// owns the session listens with <see cref="SingleInstanceServer"/>.
/// </summary>
internal static class SingleInstance
{
    internal const string Hello = "PadSharp/1";
    internal const string Done = "ok";

    /// <summary>The pipe for this user and data folder; a portable copy with its own PADSHARP_DATA gets its own pipe.</summary>
    public static string PipeName
    {
        get
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + "|" + AppSettings.DataFolder.ToUpperInvariant()));
            return "PadSharp-" + Convert.ToHexString(hash, 0, 8);
        }
    }

    /// <summary>Sends files, as full paths, to a running PadSharp. False when none answers in time.</summary>
    public static bool TrySend(string pipeName, IReadOnlyList<string> files, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(files);
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect((int)timeout.TotalMilliseconds);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            writer.WriteLine(Hello);
            foreach (string file in files)
            {
                writer.WriteLine(Path.GetFullPath(file));
            }

            writer.WriteLine();
            return reader.ReadLine() == Done;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

/// <summary>Listens for files from later starts and hands each batch to <c>received</c> (on a background thread).</summary>
internal sealed class SingleInstanceServer : IDisposable
{
    private readonly string pipeName;
    private readonly Action<IReadOnlyList<string>> received;
    private readonly CancellationTokenSource stop = new();
    private readonly Task loop;

    public SingleInstanceServer(string pipeName, Action<IReadOnlyList<string>> received)
    {
        this.pipeName = pipeName;
        this.received = received;
        loop = Task.Run(ListenAsync);
    }

    public void Dispose()
    {
        stop.Cancel();
        try
        {
            loop.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // The loop ends by cancellation.
        }

        stop.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await ServeOneAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (IOException)
            {
                // A client that went away mid-message, or the name is taken; wait a little and listen again.
                await Task.Delay(200, stop.Token).ConfigureAwait(false);
            }
        }
    }

    private async Task ServeOneAsync()
    {
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync(stop.Token).ConfigureAwait(false);
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\n" };
        if (await reader.ReadLineAsync(stop.Token).ConfigureAwait(false) != SingleInstance.Hello)
        {
            return;
        }

        var files = new List<string>();
        while (await reader.ReadLineAsync(stop.Token).ConfigureAwait(false) is { Length: > 0 } line)
        {
            files.Add(line);
        }

        received(files);
        await writer.WriteLineAsync(SingleInstance.Done.AsMemory(), stop.Token).ConfigureAwait(false);
    }
}
