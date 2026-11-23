using Avalonia.Controls;

namespace PadSharp;

/// <summary>Hot exit (session save and restore) and noticing files changed outside the editor.</summary>
internal sealed partial class MainWindow
{
    /// <summary>Reopens the tabs of the last run: unsaved text from the buffers folder, the rest from disk.</summary>
    private void RestoreSession()
    {
        if (Session.Load() is not { } state)
        {
            return;
        }

        foreach (SessionTab tab in state.Tabs)
        {
            if (RestoreTab(tab) is { } doc)
            {
                AddDocument(doc);
            }
        }

        if (documents.Count > 0)
        {
            tabs.SelectedIndex = Math.Clamp(state.Selected, 0, documents.Count - 1);
        }
    }

    /// <summary>One remembered tab, or null when it can't come back (buffer and file both gone or unreadable).</summary>
    private static Document? RestoreTab(SessionTab tab)
    {
        // A saved language is a manual choice; without one the language is detected again.
        Language? chosen = Languages.All.FirstOrDefault(l => l.Name == tab.Language);
        if (tab.Buffer is { } buffer && Session.ReadBuffer(buffer) is { } text)
        {
            var unsaved = new Document(tab.Path is null ? Math.Max(1, tab.UntitledNumber) : 0);
            unsaved.Restore(tab.Path, text, tab.Encoding, tab.Eol, chosen ?? Languages.Detect(tab.Path, text), tab.Caret);
            return unsaved;
        }

        if (tab.Path is not { } path || !File.Exists(path))
        {
            return null;
        }

        var doc = new Document(0);
        try
        {
            doc.LoadFrom(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        doc.Language = chosen ?? doc.Language;
        doc.Editor.CaretOffset = Math.Clamp(tab.Caret, 0, doc.Editor.Document.TextLength);
        return doc;
    }

    private void SaveSessionIfPending()
    {
        if (sessionPending)
        {
            SaveSession();
        }
    }

    /// <summary>Writes every tab to the session; unsaved text goes to the buffers folder, rewritten only when it changed.</summary>
    private void SaveSession()
    {
        sessionPending = false;
        var state = new SessionState();
        var changed = new List<(string Name, string Text)>();
        foreach (Document doc in tabs.Items.OfType<TabItem>().Select(t => t.Tag).OfType<Document>().Where(d => !d.IsPristineUntitled))
        {
            if (doc == Current)
            {
                state.Selected = state.Tabs.Count;
            }

            state.Tabs.Add(new SessionTab
            {
                Path = doc.FilePath,
                UntitledNumber = doc.UntitledNumber,
                Buffer = TrackBuffer(doc, changed),
                Encoding = doc.Encoding,
                Eol = doc.Eol,
                // Only a manual choice is stored, so a later, better detection (e.g. a new language) takes effect.
                Language = doc.Language == Languages.Detect(doc.FilePath, doc.FirstLine) ? null : doc.Language.Name,
                Caret = doc.Editor.CaretOffset,
            });
        }

        Session.Save(state, changed);
    }

    /// <summary>
    /// The buffer name for a tab with unsaved changes (queuing its text when it changed since the last save),
    /// or null for a saved tab.
    /// </summary>
    private string? TrackBuffer(Document doc, List<(string Name, string Text)> changed)
    {
        if (!doc.IsDirty)
        {
            // Its buffer file is deleted by this save; a later change must write it again.
            savedRevisions.Remove(doc);
            return null;
        }

        if (!savedRevisions.TryGetValue(doc, out int revision) || revision != doc.Revision)
        {
            changed.Add((doc.BufferName, doc.Text));
            savedRevisions[doc] = doc.Revision;
        }

        return doc.BufferName;
    }

    /// <summary>
    /// On activation: files changed elsewhere are reloaded silently, unless they have unsaved changes here,
    /// in which case the person chooses.
    /// </summary>
    internal async Task CheckDiskChangesAsync()
    {
        if (checkingDisk)
        {
            return;
        }

        checkingDisk = true;
        try
        {
            foreach (Document doc in documents.Where(d => d.IsChangedOnDisk).ToList())
            {
                if (doc.FilePath is { } path && await ShouldReloadAsync(doc))
                {
                    Reload(doc, path);
                }
            }
        }
        finally
        {
            checkingDisk = false;
        }

        UpdateStatus();
    }

    /// <summary>A saved tab always reloads; one with unsaved changes asks, and keeping them stops further asking.</summary>
    private async Task<bool> ShouldReloadAsync(Document doc)
    {
        if (!doc.IsDirty)
        {
            return true;
        }

        doc.AcceptDiskVersion();
        tabs.SelectedItem = doc.Tab;
        string? answer = await Dialogs.AskAsync(this, AppName,
            $"\"{doc.Title}\" was changed outside {AppName}. Reload it and lose your changes?", "Reload", "Keep Mine");
        return answer == "Reload";
    }

    private static void Reload(Document doc, string path)
    {
        try
        {
            doc.LoadFrom(path, detectLanguage: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            doc.AcceptDiskVersion();
        }
    }
}
