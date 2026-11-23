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

    /// <summary>The file's last-write time when it was last loaded or saved here, to notice changes made while closed.</summary>
    public DateTime? DiskStamp { get; set; }

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
    private static FileStream? ownerLock;
    private static bool taken;

    private static string Folder => AppSettings.DataFolder;

    private static string StateFile => System.IO.Path.Combine(Folder, "session.json");

    private static string BufferFolder => System.IO.Path.Combine(Folder, "buffers");

    /// <summary>
    /// Makes the calling window the one that keeps the session: true once per claim, whether this process claimed
    /// it earlier (Program.Main does, before any window exists) or claims it now.
    /// </summary>
    public static bool TryTakeOwnership()
    {
        if (taken || (ownerLock is null && !TryClaim()))
        {
            return false;
        }

        taken = true;
        return true;
    }

    /// <summary>Takes ownership of the session; false when another instance (or window) has it.</summary>
    public static bool TryClaim()
    {
        if (ownerLock is not null)
        {
            return false;
        }

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

    /// <summary>Gives up ownership, so another instance can take the session.</summary>
    public static void Release()
    {
        ownerLock?.Dispose();
        ownerLock = null;
        taken = false;
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
    /// Writes changed buffers, then the state file, then deletes buffers the state no longer names. Every file is
    /// written to a temporary name and renamed into place, so a crash mid-write never leaves a half-written copy.
    /// Returns why saving failed, or null when it worked.
    /// </summary>
    public static string? Save(SessionState state, IEnumerable<(string Name, string Text)> changedBuffers)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(changedBuffers);
        try
        {
            Directory.CreateDirectory(BufferFolder);
            foreach ((string name, string text) in changedBuffers)
            {
                WriteAtomically(System.IO.Path.Combine(BufferFolder, name), text);
            }

            WriteAtomically(StateFile, JsonSerializer.Serialize(state, SettingsJson.Default.SessionState));

            var keep = state.Tabs.Select(t => t.Buffer).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.EnumerateFiles(BufferFolder))
            {
                if (!keep.Contains(System.IO.Path.GetFileName(file)))
                {
                    File.Delete(file);
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }

    private static void WriteAtomically(string path, string text)
    {
        string temp = path + ".tmp";
        File.WriteAllText(temp, text);
        File.Move(temp, path, overwrite: true);
    }
}
