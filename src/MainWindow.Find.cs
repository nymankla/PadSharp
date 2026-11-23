using AvaloniaEdit;

namespace PadSharp;

/// <summary>Acting on the find bar: find next/previous, replace, replace all, and reporting the result.</summary>
internal sealed partial class MainWindow
{
    private void ShowFind(bool replace) => findBar.Open(replace, Current?.Editor.SelectedText ?? string.Empty);

    private void FindAgain(bool backward)
    {
        if (findBar.Query.Length == 0)
        {
            ShowFind(replace: false);
            return;
        }

        if (TextSearch.Problem(findBar.Query, findBar.Options) is { } problem)
        {
            Report(problem, problem: true);
        }
        else if (Current is { } doc)
        {
            Find(doc, findBar.Query, findBar.Options with { Backward = backward }, incremental: false);
        }
    }

    /// <summary>Shows a find result in the bar, or in the status bar when the bar is closed (F3).</summary>
    private void Report(string message, bool problem)
    {
        if (findBar.IsVisible)
        {
            findBar.SetStatus(message, problem);
        }
        else if (message.Length > 0)
        {
            transientMessage = message;
            UpdateStatus();
        }
    }

    /// <summary>"3 of 12" when <paramref name="at"/> is a match, otherwise the total.</summary>
    private void ReportMatches(Document doc, string query, SearchOptions options, int at)
    {
        List<int> matches = TextSearch.Matches(doc.Text, query, options);
        int index = matches.BinarySearch(at);
        string message = matches.Count == 0 ? "No results"
            : index >= 0 ? $"{FormatNumber(index + 1)} of {FormatNumber(matches.Count)}"
            : matches.Count == 1 ? "1 match" : $"{FormatNumber(matches.Count)} matches";
        Report(message, matches.Count == 0);
    }

    private static void SelectAndShow(TextEditor editor, int start, int length)
    {
        editor.Select(start, length);
        editor.TextArea.Caret.BringCaretToView();
    }

    /// <summary>Selects the next match. Incremental searches start at the selection start, so typing extends the match in place.</summary>
    private void Find(Document doc, string query, SearchOptions options, bool incremental)
    {
        TextEditor editor = doc.Editor;
        int from = incremental ? editor.SelectionStart : editor.SelectionStart + editor.SelectionLength;
        if (TextSearch.Find(doc.Text, query, editor.SelectionStart, from, options) is { } found)
        {
            SelectAndShow(editor, found.Index, found.Length);
            ReportMatches(doc, query, options, found.Index);
        }
        else
        {
            Report("No results", problem: true);
        }
    }

    private void OnFindRequested(object? sender, FindRequestEventArgs e)
    {
        if (Current is not { } doc)
        {
            return;
        }

        if (e.Query.Length == 0)
        {
            findBar.SetStatus(string.Empty, problem: false);
            return;
        }

        if (TextSearch.Problem(e.Query, e.Options) is { } problem)
        {
            Report(problem, problem: true);
            return;
        }

        switch (e.Action)
        {
            case FindAction.Incremental:
            case FindAction.Next:
                Find(doc, e.Query, e.Options, incremental: e.Action == FindAction.Incremental);
                break;
            case FindAction.Previous:
                Find(doc, e.Query, e.Options with { Backward = true }, incremental: false);
                break;
            case FindAction.Replace:
                ReplaceCurrent(doc, e);
                break;
            case FindAction.ReplaceAll:
                ReplaceAll(doc, e);
                break;
            default:
                break;
        }
    }

    /// <summary>Replaces the selection when it is a match (filling in <c>$1</c> etc. for a regex), then selects the next one.</summary>
    private void ReplaceCurrent(Document doc, FindRequestEventArgs e)
    {
        TextEditor editor = doc.Editor;
        int start = editor.SelectionStart;
        if (TextSearch.ReplacementAt(doc.Text, e.Query, start, editor.SelectionLength, e.Replacement, e.Options) is { } replacement)
        {
            editor.Document.Replace(start, editor.SelectionLength, replacement);
            editor.Select(start + replacement.Length, 0);
        }

        Find(doc, e.Query, e.Options, incremental: false);
    }

    private void ReplaceAll(Document doc, FindRequestEventArgs e)
    {
        (string text, int total) = TextSearch.ReplaceAll(doc.Text, e.Query, e.Replacement, e.Options);
        if (total > 0)
        {
            doc.ReplaceAllText(text);
        }

        Report(total == 0 ? "No results" : $"Replaced {FormatNumber(total)}", problem: total == 0);
    }
}
