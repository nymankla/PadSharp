using Avalonia.Input;
using Avalonia.Interactivity;
using Path = System.IO.Path;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using AvaloniaEdit;

namespace PadSharp;

/// <summary>Tabs and files: new, open, reload, save, close, and drag and drop.</summary>
internal sealed partial class MainWindow
{
    private Document AddDocument(Document doc)
    {
        documents.Add(doc);
        doc.ApplySettings(settings, zoom);
        doc.Changed += (_, _) => OnDocumentChanged(doc);
        doc.CloseRequested += (_, _) => Run(() => CloseDocumentAsync(doc));
        TextEditor editor = doc.Editor;
        editor.TextArea.Caret.PositionChanged += (_, _) => UpdateStatus();
        editor.TextArea.SelectionChanged += (_, _) => UpdateStatus();
        editor.TextArea.KeyUp += OnEditorKeyUp;
        editor.AddHandler(PointerWheelChangedEvent, OnEditorWheel, RoutingStrategies.Tunnel);
        editor.ContextMenu = BuildEditorMenu();
        tabs.Items.Add(doc.Tab);
        return doc;
    }

    private void OnDocumentChanged(Document doc)
    {
        sessionPending = true;
        if (doc == Current)
        {
            UpdateStatus();
        }
    }

    /// <summary>Insert toggles overtype without moving the caret or changing text, so nothing else refreshes "OVR".</summary>
    private void OnEditorKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Insert)
        {
            UpdateStatus();
        }
    }

    /// <summary>Ctrl (Cmd on macOS) + mouse wheel zooms.</summary>
    private void OnEditorWheel(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(CommandKey))
        {
            ChangeZoom(e.Delta.Y > 0 ? 1 : -1);
            e.Handled = true;
        }
    }

    private int NextUntitledNumber()
    {
        int n = 1;
        while (documents.Exists(d => d.UntitledNumber == n))
        {
            n++;
        }

        return n;
    }

    private void NewDocument()
    {
        Document doc = AddDocument(new Document(NextUntitledNumber()));
        tabs.SelectedItem = doc.Tab;
        UpdateStatus();
    }

    private static FilePickerFileType FileTypeOf(Language language) =>
        new($"{language.Name} (*.{string.Join(";*.", language.Extensions)})") { Patterns = language.Patterns };

    /// <summary>"All types" then one filter per language; <paramref name="first"/>, when given, leads the list.</summary>
    private static FilePickerFileType[] FileTypes(Language? first = null)
    {
        FilePickerFileType all = new("All types (*.*)") { Patterns = ["*"] };
        IEnumerable<FilePickerFileType> rest = Languages.All.Where(l => l != first).Select(FileTypeOf);
        return first is null ? [all, .. rest] : [FileTypeOf(first), all, .. rest];
    }

    private async Task OpenWithDialogAsync()
    {
        IReadOnlyList<IStorageFile> picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open",
            AllowMultiple = true,
            FileTypeFilter = FileTypes(),
        });
        OpenFiles(picked.Select(f => f.TryGetLocalPath()).OfType<string>().ToList());
    }

    private void OpenFiles(IReadOnlyList<string> paths)
    {
        foreach (string raw in paths)
        {
            OpenFile(Path.GetFullPath(raw));
        }

        UpdateStatus();
    }

    /// <summary>
    /// Brings an already open file to the front, or opens it in a new tab; the first file replaces an empty untitled
    /// tab. A missing or unreadable file gets a message instead.
    /// </summary>
    private void OpenFile(string path)
    {
        if (documents.Find(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            tabs.SelectedItem = existing.Tab;
            return;
        }

        if (LoadForTab(path) is not { } doc)
        {
            return;
        }

        Document? pristine = documents.Count == 1 && documents[0].IsPristineUntitled ? documents[0] : null;
        AddDocument(doc);
        tabs.SelectedItem = doc.Tab;
        if (pristine is not null)
        {
            RemoveDocument(pristine);
        }
    }

    private Document? LoadForTab(string path)
    {
        if (!File.Exists(path))
        {
            ShowMessage("Open", $"\"{path}\" doesn't exist.");
            return null;
        }

        var doc = new Document(0);
        try
        {
            doc.LoadFrom(path);
            return doc;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowMessage("Open", $"Cannot open file \"{path}\".\n{ex.Message}");
            return null;
        }
    }

    private async Task ReloadCurrentAsync()
    {
        if (Current is not { FilePath: { } path } doc)
        {
            return;
        }

        if (doc.IsDirty && await Dialogs.AskAsync(this, "Reload",
                $"Reload \"{doc.Title}\" from disk and lose your changes?", "Reload", "Cancel") != "Reload")
        {
            return;
        }

        try
        {
            doc.LoadFrom(path, detectLanguage: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.AskAsync(this, "Reload", ex.Message, "OK");
        }
    }

    private async Task<bool> SaveDocumentAsync(Document? doc)
    {
        if (doc is null)
        {
            return false;
        }

        return doc.FilePath is null ? await SaveDocumentAsAsync(doc) : await WriteDocumentAsync(doc, doc.FilePath);
    }

    private async Task<bool> SaveDocumentAsAsync(Document? doc)
    {
        if (doc is null)
        {
            return false;
        }

        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save As",
            SuggestedFileName = doc.FilePath is null ? doc.Title : Path.GetFileName(doc.FilePath),
            FileTypeChoices = FileTypes(doc.Language),
            DefaultExtension = doc.Language.Extensions[0],
            ShowOverwritePrompt = true,
        });
        return file?.TryGetLocalPath() is { } path && await WriteDocumentAsync(doc, path);
    }

    private async Task<bool> WriteDocumentAsync(Document doc, string path)
    {
        try
        {
            doc.SaveTo(path);
            UpdateStatus();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await Dialogs.AskAsync(this, "Save failed", $"Cannot save file \"{path}\".\n{ex.Message}", "OK");
            return false;
        }
    }

    /// <summary>Saves every changed tab in turn; stops at the first that isn't saved (e.g. Save As cancelled).</summary>
    private async Task SaveAllAsync()
    {
        foreach (Document doc in documents.Where(d => d.IsDirty).ToList())
        {
            tabs.SelectedItem = doc.Tab;
            if (!await SaveDocumentAsync(doc))
            {
                return;
            }
        }
    }

    /// <summary>Asks whether to save a changed document. False means the person cancelled.</summary>
    private async Task<bool> ConfirmDiscardAsync(Document doc)
    {
        tabs.SelectedItem = doc.Tab;
        string? answer = await Dialogs.AskAsync(this, AppName, $"Save changes to \"{doc.FullName}\"?", "Save", "Don't Save", "Cancel");
        return answer switch
        {
            "Save" => await SaveDocumentAsync(doc),
            "Don't Save" => true,
            _ => false,
        };
    }

    private async Task ConfirmCloseAsync()
    {
        foreach (Document doc in documents.ToList())
        {
            if (doc.IsDirty && !await ConfirmDiscardAsync(doc))
            {
                return;
            }
        }

        closeConfirmed = true;
        Close();
    }

    private async Task<bool> CloseDocumentAsync(Document? doc)
    {
        if (doc is null)
        {
            return false;
        }

        if (doc.IsDirty && !await ConfirmDiscardAsync(doc))
        {
            return false;
        }

        RemoveDocument(doc);
        if (documents.Count == 0)
        {
            NewDocument();
        }

        UpdateStatus();
        return true;
    }

    private async Task CloseAllAsync()
    {
        foreach (Document doc in documents.ToList())
        {
            if (!await CloseDocumentAsync(doc))
            {
                return;
            }
        }
    }

    private void RemoveDocument(Document doc)
    {
        tabs.Items.Remove(doc.Tab);
        documents.Remove(doc);
        savedRevisions.Remove(doc);
        sessionPending = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 })
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.TryGetFiles() is { Length: > 0 } items)
        {
            OpenFiles(items.Select(i => i.TryGetLocalPath()).OfType<string>().ToList());
            e.Handled = true;
        }
    }
}
