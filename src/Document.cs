using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace PadSharp;

/// <summary>One open file (or untitled buffer) shown in its own tab.</summary>
internal sealed class Document
{
    private readonly TextBlock titleBlock = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 6, 0) };
    private readonly Ellipse dot = new() { Width = 7, Height = 7, VerticalAlignment = VerticalAlignment.Center };
    private bool forcedDirty;

    public Document(int untitledNumber)
    {
        UntitledNumber = untitledNumber;
        Editor = new TextEditor
        {
            FontFamily = Look.EditorFontFor(Look.DefaultFontName),
            FontSize = Look.PtToDip(Look.DefaultFontSizePt),
            Background = Look.EditorBack,
            Foreground = Look.EditorFore,
            ShowLineNumbers = false,
            WordWrap = false,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Visible,
        };
        Editor.Options.IndentationSize = Look.DefaultTabWidth;
        Editor.Options.ConvertTabsToSpaces = false;
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.AllowToggleOverstrikeMode = true;
        Editor.TextArea.TextView.CurrentLineBackground = Look.CurrentLineBack;
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(Look.CurrentLineBack);
        Editor.TextArea.SelectionBrush = Look.SelectionBack;
        Editor.TextArea.SelectionForeground = null;
        Editor.TextArea.SelectionBorder = null;
        Editor.TextArea.SelectionCornerRadius = 3;
        Editor.TextArea.Caret.CaretBrush = Look.Caret;
        Editor.Padding = new Thickness(0, 6, 0, 0);

        Gutter = new LineNumberGutter();
        Editor.TextArea.LeftMargins.Insert(0, Gutter);
        Editor.TextArea.Caret.PositionChanged += (_, _) => Gutter.CurrentLine = Editor.TextArea.Caret.Line;

        Editor.Document.TextChanged += (_, _) =>
        {
            Revision++;
            Changed?.Invoke(this, EventArgs.Empty);
        };
        Editor.Document.UndoStack.PropertyChanged += OnUndoStackChanged;

        var close = new Button
        {
            Content = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse("M0,0 L7,7 M7,0 L0,7"),
                Stroke = Look.MutedFore,
                StrokeThickness = 1.4,
                StrokeLineCap = PenLineCap.Round,
            },
            Padding = new Thickness(5),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
        };
        close.Click += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        var header = new StackPanel { Orientation = Orientation.Horizontal, Children = { dot, titleBlock, close } };
        header.PointerReleased += (_, e) =>
        {
            if (e.InitialPressMouseButton == MouseButton.Middle)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        };
        Tab = new TabItem { Header = header, Content = Editor, Tag = this };
        UpdateTitle();
    }

    /// <summary>Raised when the text or the saved state changes.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised when the tab's close button (or a middle click) asks to close it.</summary>
    public event EventHandler? CloseRequested;

    public TextEditor Editor { get; }

    public LineNumberGutter Gutter { get; }

    public TabItem Tab { get; }

    public string? FilePath { get; private set; }

    /// <summary>The number in "Untitled-N" for an untitled buffer, 0 once it has a file.</summary>
    public int UntitledNumber { get; private set; }

    /// <summary>Stable name for this tab's unsaved text in the session's buffers folder.</summary>
    public string BufferName { get; } = Guid.NewGuid().ToString("N") + ".txt";

    /// <summary>Counts text changes, so the session only rewrites buffers that changed.</summary>
    public int Revision { get; private set; }

    /// <summary>The file's last-write time when it was loaded or saved, to notice changes made elsewhere.</summary>
    private DateTime? DiskStamp { get; set; }

    /// <summary>True when the file was rewritten by something else since it was loaded or saved here.</summary>
    public bool IsChangedOnDisk =>
        FilePath is { } path && DiskStamp is { } stamp && File.Exists(path) && File.GetLastWriteTimeUtc(path) != stamp;

    public bool IsDirty => forcedDirty || !Editor.Document.UndoStack.IsOriginalFile;

    public EolKind Eol { get; set; } = EolKind.Windows;

    public TextEncodingKind Encoding { get; set; } = TextEncodingKind.Utf8;

    /// <summary>The language used for colouring and shown in the status bar.</summary>
    public Language Language
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
            Editor.SyntaxHighlighting = Languages.DefinitionFor(value);
        }
    } = Languages.NormalText;

    public string Text => Editor.Document.Text;

    public string Title => FilePath is null ? $"Untitled-{UntitledNumber}" : System.IO.Path.GetFileName(FilePath);

    public string FullName => FilePath ?? Title;

    public int LineCount => Editor.Document.LineCount;

    /// <summary>True for an untitled buffer nobody has typed in yet.</summary>
    public bool IsPristineUntitled => FilePath is null && !IsDirty && Editor.Document.TextLength == 0;

    /// <summary>1-based line number of an offset.</summary>
    public int LineOf(int offset) => Editor.Document.GetLineByOffset(offset).LineNumber;

    /// <summary>Column of an offset, counting a tab as reaching the next tab stop (1-based).</summary>
    public int ColumnOf(int offset)
    {
        DocumentLine line = Editor.Document.GetLineByOffset(offset);
        string before = Editor.Document.GetText(line.Offset, offset - line.Offset);
        int col = 0;
        foreach (char c in before)
        {
            col = c == '\t' ? ((col / TabWidth) + 1) * TabWidth : col + 1;
        }

        return col + 1;
    }

    /// <summary>Loads a file. A first load picks the language; a reload keeps the language and the caret.</summary>
    public void LoadFrom(string path, bool detectLanguage = true)
    {
        LoadedText loaded = TextFile.Load(path);
        int caret = detectLanguage ? 0 : Editor.CaretOffset;
        Eol = loaded.Eol;
        Encoding = loaded.Encoding;
        FilePath = path;
        UntitledNumber = 0;
        Editor.Document.Text = loaded.Text;
        Editor.Document.UndoStack.ClearAll();
        Editor.CaretOffset = Math.Min(caret, Editor.Document.TextLength);
        if (detectLanguage)
        {
            Language = Languages.Detect(path, loaded.Text);
        }

        DiskStamp = File.GetLastWriteTimeUtc(path);
        MarkSaved();
    }

    /// <summary>Brings back a tab's unsaved text from the session; it stays marked as changed.</summary>
    public void Restore(string? path, string text, TextEncodingKind encoding, EolKind eol, Language language, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);
        FilePath = path;
        if (path is not null)
        {
            UntitledNumber = 0;
            DiskStamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : null;
        }

        Encoding = encoding;
        Eol = eol;
        Language = language;
        Editor.Document.Text = text;
        Editor.Document.UndoStack.ClearAll();
        Editor.CaretOffset = Math.Clamp(caret, 0, text.Length);
        MarkDirty();
    }

    /// <summary>Stops reporting the current on-disk version as a change (the person chose to keep theirs).</summary>
    public void AcceptDiskVersion()
    {
        if (FilePath is { } path && File.Exists(path))
        {
            DiskStamp = File.GetLastWriteTimeUtc(path);
        }
    }

    /// <summary>Saves to a path. Saving under a new name switches to the language its extension selects.</summary>
    public void SaveTo(string path)
    {
        TextFile.Save(path, TextFile.NormalizeEol(Text), Encoding, Eol);
        if (!string.Equals(path, FilePath, StringComparison.OrdinalIgnoreCase) && Languages.FromExtension(path) is { } byExtension)
        {
            Language = byExtension;
        }

        FilePath = path;
        UntitledNumber = 0;
        DiskStamp = File.GetLastWriteTimeUtc(path);
        MarkSaved();
    }

    /// <summary>Replaces the whole text as one step that can be undone.</summary>
    public void ReplaceAllText(string text)
    {
        int caret = Editor.CaretOffset;
        Editor.Document.Replace(0, Editor.Document.TextLength, text);
        Editor.CaretOffset = Math.Min(caret, Editor.Document.TextLength);
    }

    /// <summary>Marks a change that isn't in the text itself (line endings, encoding).</summary>
    public void MarkDirty()
    {
        forcedDirty = true;
        UpdateTitle();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Columns a tab reaches to; follows the Tab width setting.</summary>
    public int TabWidth => Editor.Options.IndentationSize;

    /// <summary>Applies the editor preferences and the current zoom level.</summary>
    public void ApplySettings(AppSettings settings, int zoom)
    {
        ArgumentNullException.ThrowIfNull(settings);
        FontFamily font = Look.EditorFontFor(settings.FontName);
        double size = Look.PtToDip(Look.FontSizeForZoomPt(settings.FontSizePt, zoom));
        Editor.FontFamily = font;
        Editor.FontSize = size;
        Gutter.NumberFont = font;
        Gutter.NumberFontSize = size;
        Gutter.IsVisible = settings.ShowLineNumbers;
        Editor.WordWrap = settings.WordWrap;
        Editor.Options.IndentationSize = settings.TabWidth;
        Editor.Options.ConvertTabsToSpaces = settings.InsertSpaces;
        Editor.Options.HighlightCurrentLine = settings.HighlightCurrentLine;
        Editor.Options.ShowSpaces = settings.ShowWhitespace;
        Editor.Options.ShowTabs = settings.ShowWhitespace;
    }

    /// <summary>Re-fetches syntax colours and repaints after the palette changed.</summary>
    public void RefreshTheme()
    {
        Editor.SyntaxHighlighting = Languages.DefinitionFor(Language);
        Editor.TextArea.TextView.Redraw();
        Gutter.InvalidateVisual();
        UpdateTitle();
    }

    private void MarkSaved()
    {
        forcedDirty = false;
        Editor.Document.UndoStack.MarkAsOriginalFile();
        UpdateTitle();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnUndoStackChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UndoStack.IsOriginalFile))
        {
            UpdateTitle();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UpdateTitle()
    {
        titleBlock.Text = Title;
        dot.Fill = IsDirty ? Look.UnsavedDot : Look.SavedDot;
        ToolTip.SetTip(Tab, FullName);
    }
}
