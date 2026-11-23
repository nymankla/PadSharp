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

        if (TextSearch.Problem(findBar.Search) is { } problem)
        {
            Report(problem, problem: true);
        }
        else if (Current is { } doc)
        {
            Find(doc, findBar.Search with { Backward = backward }, incremental: false);
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
    private void ReportMatches(Document doc, SearchQuery search, int at)
    {
        List<int> matches = TextSearch.Matches(doc.Text, search);
        Report(MatchesText(matches, matches.BinarySearch(at)), matches.Count == 0);
    }

    /// <summary>"No results", "3 of 12" (the match at <paramref name="index"/>), "1 match" or "12 matches".</summary>
    private static string MatchesText(List<int> matches, int index)
    {
        if (matches.Count == 0)
        {
            return "No results";
        }

        if (index >= 0)
        {
            return $"{FormatNumber(index + 1)} of {FormatNumber(matches.Count)}";
        }

        return matches.Count == 1 ? "1 match" : $"{FormatNumber(matches.Count)} matches";
    }

    private static void SelectAndShow(TextEditor editor, int start, int length)
    {
        editor.Select(start, length);
        editor.TextArea.Caret.BringCaretToView();
    }

    /// <summary>Selects the next match. Incremental searches start at the selection start, so typing extends the match in place.</summary>
    private void Find(Document doc, SearchQuery search, bool incremental)
    {
        TextEditor editor = doc.Editor;
        int from = incremental ? editor.SelectionStart : editor.SelectionStart + editor.SelectionLength;
        if (TextSearch.Find(doc.Text, search, editor.SelectionStart, from) is { } found)
        {
            SelectAndShow(editor, found.Index, found.Length);
            ReportMatches(doc, search, found.Index);
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

        // An empty query clears the status; an invalid regex says so.
        string? problem = e.Query.Length == 0 ? string.Empty : TextSearch.Problem(e.Search);
        if (problem is not null)
        {
            findBar.SetStatus(problem, problem: problem.Length > 0);
            return;
        }

        Act(doc, e);
    }

    private void Act(Document doc, FindRequestEventArgs e)
    {
        switch (e.Action)
        {
            case FindAction.Incremental:
            case FindAction.Next:
                Find(doc, e.Search, incremental: e.Action == FindAction.Incremental);
                break;
            case FindAction.Previous:
                Find(doc, e.Search with { Backward = true }, incremental: false);
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
        if (TextSearch.ReplacementAt(doc.Text, e.Search, new SearchHit(start, editor.SelectionLength), e.Replacement) is { } replacement)
        {
            editor.Document.Replace(start, editor.SelectionLength, replacement);
            editor.Select(start + replacement.Length, 0);
        }

        Find(doc, e.Search, incremental: false);
    }

    private void ReplaceAll(Document doc, FindRequestEventArgs e)
    {
        (string text, int total) = TextSearch.ReplaceAll(doc.Text, e.Search, e.Replacement);
        if (total > 0)
        {
            doc.ReplaceAllText(text);
        }

        Report(total == 0 ? "No results" : $"Replaced {FormatNumber(total)}", problem: total == 0);
    }
}
