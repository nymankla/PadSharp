using System.Globalization;
using AvaloniaEdit.Document;

namespace PadSharp;

/// <summary>Text commands on the tab in front: case, line operations, line endings, encoding, language, go to line.</summary>
internal sealed partial class MainWindow
{
    private void ConvertCase(bool upper)
    {
        if (Current?.Editor is not { SelectionLength: > 0 } editor)
        {
            return;
        }

        int start = editor.SelectionStart;
        string selected = editor.SelectedText;
        string converted = upper ? selected.ToUpper(CultureInfo.CurrentCulture) : selected.ToLower(CultureInfo.CurrentCulture);
        editor.Document.Replace(start, selected.Length, converted);
        editor.Select(start, converted.Length);
    }

    private void DuplicateLine()
    {
        if (Current is not { } doc)
        {
            return;
        }

        TextDocument text = doc.Editor.Document;
        DocumentLine line = text.GetLineByOffset(doc.Editor.CaretOffset);
        string newline = TextUtilities.GetNewLineFromDocument(text, line.LineNumber);
        text.Insert(line.EndOffset, newline + text.GetText(line.Offset, line.Length));
    }

    private void DeleteLine()
    {
        if (Current is not { } doc)
        {
            return;
        }

        TextDocument text = doc.Editor.Document;
        DocumentLine line = text.GetLineByOffset(doc.Editor.CaretOffset);
        int start = line.Offset;
        int length = line.TotalLength;
        if (line.DelimiterLength == 0 && line.PreviousLine is { } previous)
        {
            start = previous.EndOffset;
            length = text.TextLength - start;
        }

        text.Remove(start, length);
    }

    private void SetEol(EolKind eol)
    {
        if (Current is { } doc && (doc.Eol != eol || doc.HasMixedEol))
        {
            doc.Eol = eol;
            doc.HasMixedEol = false;
            doc.MarkDirty();
        }
    }

    private void SetEncoding(TextEncodingKind encoding)
    {
        if (Current is { } doc && doc.Encoding != encoding)
        {
            doc.Encoding = encoding;
            doc.MarkDirty();
        }
    }

    private void SetLanguage(Language language)
    {
        if (Current is { } doc)
        {
            doc.Language = language;
        }

        UpdateStatus();
    }

    private async Task GoToLineAsync()
    {
        if (Current is not { } doc)
        {
            return;
        }

        int? line = await Dialogs.GoToLineAsync(this, doc.LineOf(doc.Editor.CaretOffset), doc.LineCount);
        if (line is { } target)
        {
            doc.Editor.TextArea.Caret.Line = target;
            doc.Editor.TextArea.Caret.Column = 1;
            doc.Editor.Select(doc.Editor.CaretOffset, 0);
            doc.Editor.TextArea.Caret.BringCaretToView();
            doc.Editor.TextArea.Focus();
        }
    }
}
