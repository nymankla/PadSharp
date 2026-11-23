using System.Text.Json;

namespace PadSharp;

/// <summary>One tab as remembered between runs.</summary>
internal sealed class SessionTab
{
    /// <summary>The file, or null for an untitled buffer.</summary>
    public string? Path { get; set; }

    public int UntitledNumber { get; set; }

    /// <summary>Name of the file in the buffers folder holding unsaved text; null when the tab has no unsaved changes.</summary>
    public string? Buffer { get; set; }

    public TextEncodingKind Encoding { get; set; } = TextEncodingKind.Utf8;

    public EolKind Eol { get; set; } = EolKind.Windows;

    public string? Language { get; set; }

    public int Caret { get; set; }
}

/// <summary>The open tabs, saved continuously so closing (or a crash) never loses a scratch buffer.</summary>
internal sealed class SessionState
{
    public List<SessionTab> Tabs { get; set; } = [];

    public int Selected { get; set; }
}

/// <summary>
/// Hot exit: the session lives next to settings.json. Only one running instance owns it (a lock file);
/// any other instance runs without it and asks to save on close.
/// </summary>
internal static class Session
{
    private static readonly string Folder = System.IO.Path.GetDirectoryName(AppSettings.FilePath)!;
    private static readonly string StateFile = System.IO.Path.Combine(Folder, "session.json");
    private static readonly string BufferFolder = System.IO.Path.Combine(Folder, "buffers");
    private static FileStream? ownerLock;

    /// <summary>Takes ownership of the session; false when another instance has it.</summary>
    public static bool TryClaim()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            ownerLock = new FileStream(System.IO.Path.Combine(Folder, "session.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static SessionState? Load()
    {
        try
        {
            return File.Exists(StateFile) ? JsonSerializer.Deserialize(File.ReadAllText(StateFile), SettingsJson.Default.SessionState) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Console.Error.WriteLine($"Session not loaded: {ex.Message}");
            return null;
        }
    }

    public static string? ReadBuffer(string name)
    {
        string path = System.IO.Path.Combine(BufferFolder, System.IO.Path.GetFileName(name));
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes changed buffers, then the state file (atomically), then deletes buffers the state no longer names.
    /// </summary>
    public static void Save(SessionState state, IEnumerable<(string Name, string Text)> changedBuffers)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            Directory.CreateDirectory(BufferFolder);
            foreach ((string name, string text) in changedBuffers)
            {
                File.WriteAllText(System.IO.Path.Combine(BufferFolder, name), text);
            }

            string temp = StateFile + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(state, SettingsJson.Default.SessionState));
            File.Move(temp, StateFile, overwrite: true);

            var keep = state.Tabs.Select(t => t.Buffer).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.EnumerateFiles(BufferFolder))
            {
                if (!keep.Contains(System.IO.Path.GetFileName(file)))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Session not saved: {ex.Message}");
        }
    }
}
