using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;

namespace PadSharp;

/// <summary>The status bar, its pickers, and keeping it, the menu checks and the title in step with the current tab.</summary>
internal sealed partial class MainWindow
{
    /// <summary>Language, position, line count, line endings, encoding. Language, line endings and encoding open a picker on click.</summary>
    private Border BuildStatusBar()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto") };
        (TextBlock Text, Action? Click)[] parts =
        [
            (languageText, () => ShowPicker(languageText, Choices(Languages.All, l => l.Name, l => l == Current?.Language, SetLanguage))),
            (positionText, null),
            (linesText, null),
            (eolText, () => ShowPicker(eolText, Choices([EolKind.Windows, EolKind.Unix, EolKind.Macintosh], TextFile.EolName, k => k == Current?.Eol, SetEol))),
            (encodingText, () => ShowPicker(encodingText, Choices(Enum.GetValues<TextEncodingKind>(), TextFile.EncodingName, k => k == Current?.Encoding, SetEncoding))),
        ];
        var divider = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        for (int i = 0; i < parts.Length; i++)
        {
            Border cell = StatusCell(parts[i].Text, parts[i].Click, divider, first: i == 0);
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }

        languageText.FontWeight = FontWeight.SemiBold;
        return new Border { Child = grid, Background = Look.StatusGradient };
    }

    private static Border StatusCell(TextBlock text, Action? click, IBrush divider, bool first)
    {
        text.FontSize = 12;
        text.Foreground = Brushes.White;
        text.VerticalAlignment = VerticalAlignment.Center;
        var cell = new Border
        {
            Child = text,
            Padding = new Thickness(12, 5),
            MinWidth = first ? 0 : 70,
            Background = Brushes.Transparent,
            BorderBrush = divider,
            BorderThickness = new Thickness(first ? 0 : 1, 0, 0, 0),
            Margin = new Thickness(0, 3),
        };
        if (click is not null)
        {
            cell.Cursor = new Cursor(StandardCursorType.Hand);
            cell.PointerPressed += (_, _) => click();
        }

        return cell;
    }

    /// <summary>A pop-up list above a status-bar cell, the current choice checked.</summary>
    private static void ShowPicker(Control anchor, IEnumerable<(string Name, bool Current, Action Pick)> choices)
    {
        var menu = new ContextMenu { Placement = PlacementMode.TopEdgeAlignedLeft };
        foreach ((string name, bool current, Action pick) in choices)
        {
            menu.Items.Add(new MenuItem
            {
                Header = name.Replace("_", "__", StringComparison.Ordinal),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = current,
                Command = new RelayCommand(pick),
            });
        }

        menu.Open(anchor);
    }

    /// <summary>The entries of a picker: each value's name, whether it is the current one, and how to choose it.</summary>
    internal static IEnumerable<(string Name, bool Current, Action Pick)> Choices<T>(
        IEnumerable<T> values, Func<T, string> name, Func<T, bool> isCurrent, Action<T> pick) =>
        values.Select(v => (name(v), isCurrent(v), (Action)(() => pick(v))));

    private void UpdateStatus()
    {
        if (Current is not { } doc)
        {
            return;
        }

        positionText.Text = FormatPosition(doc);
        linesText.Text = doc.LineCount == 1 ? "1 line" : $"{FormatNumber(doc.LineCount)} lines";
        eolText.Text = doc.HasMixedEol ? $"Mixed → {TextFile.EolShortName(doc.Eol)}" : TextFile.EolShortName(doc.Eol);
        encodingText.Text = TextFile.EncodingName(doc.Encoding);
        languageText.Text = transientMessage
            ?? (sessionProblem is null ? doc.Language.Name : $"⚠ Unsaved text not protected: {sessionProblem}");
        transientMessage = null;
        SyncMenuChecks(doc);

        TextEditor editor = doc.Editor;
        saveButton.Opacity = doc.IsDirty ? 1 : 0.3;
        undoButton.Opacity = editor.CanUndo ? 1 : 0.3;
        redoButton.Opacity = editor.CanRedo ? 1 : 0.3;
        Title = $"{(doc.IsDirty ? "*" : string.Empty)}{doc.FullName} - {AppName}";
    }

    /// <summary>"Ln 12, Col 4", plus the selected character count and OVR in overtype mode.</summary>
    private static string FormatPosition(Document doc)
    {
        TextEditor editor = doc.Editor;
        int length = editor.Document.TextLength;
        int caret = Math.Min(editor.CaretOffset, length);
        int selected = Math.Min(editor.SelectionLength, length - Math.Min(editor.SelectionStart, length));
        string position = $"Ln {FormatNumber(doc.LineOf(caret))}, Col {FormatNumber(doc.ColumnOf(caret))}";
        if (selected > 0)
        {
            position += $"  ({FormatNumber(selected)} selected)";
        }

        return editor.TextArea.OverstrikeMode ? position + "  OVR" : position;
    }

    private void SyncMenuChecks(Document doc)
    {
        foreach ((Language language, MenuItem item) in languageItems)
        {
            item.IsChecked = language == doc.Language;
        }

        foreach ((EolKind eol, MenuItem item) in eolItems)
        {
            item.IsChecked = eol == doc.Eol;
        }

        foreach ((TextEncodingKind enc, MenuItem item) in encodingItems)
        {
            item.IsChecked = enc == doc.Encoding;
        }
    }
}
