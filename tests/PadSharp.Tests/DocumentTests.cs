using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;

namespace PadSharp.Tests;

public sealed class DocumentTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void ANewDocumentIsAnEmptyUntitledBuffer()
    {
        var doc = new Document(3);

        Assert.Equal("Untitled-3", doc.Title);
        Assert.Equal("Untitled-3", doc.FullName);
        Assert.True(doc.IsPristineUntitled);
        Assert.False(doc.IsDirty);
        Assert.Equal((TextEncodingKind.Utf8, EolKind.Windows), (doc.Encoding, doc.Eol));
        Assert.Same(Languages.NormalText, doc.Language);
        Assert.EndsWith(".txt", doc.BufferName, StringComparison.Ordinal);
        Assert.NotEqual(doc.BufferName, new Document(4).BufferName);
    }

    [AvaloniaFact]
    public void TypingMakesItDirtyAndCountsRevisions()
    {
        var doc = new Document(1);
        int changes = 0;
        doc.Changed += (_, _) => changes++;

        doc.Editor.Document.Insert(0, "hi");

        Assert.True(doc.IsDirty);
        Assert.False(doc.IsPristineUntitled);
        Assert.Equal(1, doc.Revision);
        Assert.True(changes > 0);
    }

    [AvaloniaFact]
    public void LoadFromReadsTheFileAndDetectsEverything()
    {
        string path = sandbox.File("script.sh", "#!/bin/sh\necho hi\n");
        var doc = new Document(1);

        doc.LoadFrom(path);

        Assert.Equal((path, 0, "script.sh"), (doc.FilePath, doc.UntitledNumber, doc.Title));
        Assert.Equal("#!/bin/sh\necho hi\n", doc.Text);
        Assert.Equal(EolKind.Unix, doc.Eol);
        Assert.Equal("Shell", doc.Language.Name);
        Assert.False(doc.IsDirty);
        Assert.False(doc.IsChangedOnDisk);
        Assert.Equal((3, "#!/bin/sh"), (doc.LineCount, doc.FirstLine));
    }

    [AvaloniaFact]
    public void ReloadKeepsTheLanguageAndTheCaret()
    {
        string path = sandbox.File("a.txt", "one two three");
        var doc = new Document(0);
        doc.LoadFrom(path);
        doc.Language = Languages.All.Single(l => l.Name == "Python");
        doc.Editor.CaretOffset = 4;

        File.WriteAllText(path, "one two three four");
        doc.LoadFrom(path, detectLanguage: false);

        Assert.Equal("Python", doc.Language.Name);
        Assert.Equal(4, doc.Editor.CaretOffset);
        Assert.Equal("one two three four", doc.Text);
    }

    [AvaloniaFact]
    public void SaveToWritesTheLineEndingsAndMarksItSaved()
    {
        var doc = new Document(1);
        doc.Editor.Document.Text = "a\nb";
        doc.Eol = EolKind.Windows;
        string path = Path.Combine(sandbox.Files, "out.txt");

        doc.SaveTo(path);

        Assert.Equal("a\r\nb", File.ReadAllText(path));
        Assert.False(doc.IsDirty);
        Assert.Equal((path, 0), (doc.FilePath, doc.UntitledNumber));
    }

    [AvaloniaFact]
    public void SavingUnderANewExtensionSwitchesTheLanguage()
    {
        var doc = new Document(1);
        doc.SaveTo(Path.Combine(sandbox.Files, "x.py"));
        Assert.Equal("Python", doc.Language.Name);

        doc.SaveTo(Path.Combine(sandbox.Files, "x.unknownext"));
        Assert.Equal("Python", doc.Language.Name);
    }

    [AvaloniaFact]
    public void RestoreBringsBackUnsavedText()
    {
        var untitled = new Document(5);
        untitled.Restore(null, "draft", TextEncodingKind.Utf8Bom, EolKind.Unix, Languages.NormalText, caret: 99);

        Assert.Equal(("draft", "Untitled-5", true), (untitled.Text, untitled.Title, untitled.IsDirty));
        Assert.Equal((TextEncodingKind.Utf8Bom, EolKind.Unix), (untitled.Encoding, untitled.Eol));
        Assert.Equal(5, untitled.Editor.CaretOffset);

        string path = sandbox.File("f.txt", "on disk");
        var file = new Document(0);
        file.Restore(path, "edited", TextEncodingKind.Utf8, EolKind.Windows, Languages.NormalText, caret: 2);

        Assert.Equal((path, "edited", true, false), (file.FilePath, file.Text, file.IsDirty, file.IsChangedOnDisk));
    }

    [AvaloniaFact]
    public void MarkDirtyCoversChangesOutsideTheText()
    {
        var doc = new Document(1);
        doc.MarkDirty();
        Assert.True(doc.IsDirty);

        doc.SaveTo(Path.Combine(sandbox.Files, "m.txt"));
        Assert.False(doc.IsDirty);
    }

    [AvaloniaFact]
    public void NoticesAFileRewrittenElsewhere()
    {
        string path = sandbox.File("w.txt", "v1");
        var doc = new Document(0);
        doc.LoadFrom(path);

        File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
        Assert.True(doc.IsChangedOnDisk);

        doc.AcceptDiskVersion();
        Assert.False(doc.IsChangedOnDisk);
    }

    [AvaloniaFact]
    public void ColumnsCountTabsToTheNextStop()
    {
        var doc = new Document(1);
        doc.Editor.Document.Text = "\tab\na\tb";

        Assert.Equal(1, doc.ColumnOf(0));
        Assert.Equal(5, doc.ColumnOf(1));
        Assert.Equal(6, doc.ColumnOf(2));
        Assert.Equal(5, doc.ColumnOf(6));
        Assert.Equal((1, 2), (doc.LineOf(0), doc.LineOf(4)));
    }

    [AvaloniaFact]
    public void ReplaceAllTextIsOneUndoStep()
    {
        var doc = new Document(1);
        doc.Editor.Document.Text = "old text";
        doc.Editor.Document.UndoStack.ClearAll();
        doc.Editor.CaretOffset = 6;

        doc.ReplaceAllText("new");

        Assert.Equal(("new", 3), (doc.Text, doc.Editor.CaretOffset));
        doc.Editor.Undo();
        Assert.Equal("old text", doc.Text);
    }

    [AvaloniaFact]
    public void ApplySettingsPushesEveryPreference()
    {
        var doc = new Document(1);
        var settings = new AppSettings
        {
            FontSizePt = 12, TabWidth = 2, InsertSpaces = true, WordWrap = true, ShowLineNumbers = false,
            HighlightCurrentLine = false, ShowWhitespace = true,
        };

        doc.ApplySettings(settings, zoom: 3);

        Assert.Equal(Look.PtToDip(15), doc.Editor.FontSize);
        Assert.Equal(Look.PtToDip(15), doc.Gutter.NumberFontSize);
        Assert.Equal(2, doc.TabWidth);
        Assert.True(doc.Editor.Options.ConvertTabsToSpaces);
        Assert.True(doc.Editor.WordWrap);
        Assert.False(doc.Gutter.IsVisible);
        Assert.False(doc.Editor.Options.HighlightCurrentLine);
        Assert.True(doc.Editor.Options.ShowSpaces && doc.Editor.Options.ShowTabs);
    }

    [AvaloniaFact]
    public void TheTabsCloseButtonAsksToClose()
    {
        var doc = new Document(1);
        bool asked = false;
        doc.CloseRequested += (_, _) => asked = true;

        Button close = Assert.IsType<StackPanel>(doc.Tab.Header).Children.OfType<Button>().Single();
        close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(asked);
        Assert.Same(doc, doc.Tab.Tag);
        Assert.Same(doc.Editor, doc.Tab.Content);
    }

    [AvaloniaFact]
    public void RefreshThemeKeepsTheColouring()
    {
        var doc = new Document(1) { Language = Languages.All.Single(l => l.Name == "Rust") };

        doc.RefreshTheme();

        Assert.Equal("Rust", doc.Editor.SyntaxHighlighting?.Name);
    }
}
