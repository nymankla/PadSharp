namespace PadSharp;

internal readonly record struct SearchOptions(bool MatchCase, bool WholeWord, bool Backward);

internal readonly record struct SearchHit(int Index, int Length);

/// <summary>Plain-text search used by the find bar. Searches always wrap around the document.</summary>
internal static class TextSearch
{
    /// <summary>
    /// Finds the next match, wrapping at the ends. Forward searches start at <paramref name="selEnd"/>;
    /// backward searches look for a match ending at or before <paramref name="selStart"/>.
    /// </summary>
    public static SearchHit? Find(string text, string query, int selStart, int selEnd, SearchOptions options)
    {
        if (query.Length == 0)
        {
            return null;
        }

        int idx = options.Backward
            ? Backward(text, query, selStart, options) is var b and >= 0 ? b : Backward(text, query, text.Length, options)
            : Forward(text, query, selEnd, text.Length, options) is var f and >= 0 ? f : Forward(text, query, 0, text.Length, options);
        return idx >= 0 ? new SearchHit(idx, query.Length) : null;
    }

    /// <summary>Start index of every match, in order.</summary>
    public static List<int> Matches(string text, string query, SearchOptions options)
    {
        var found = new List<int>();
        if (query.Length == 0)
        {
            return found;
        }

        int pos = 0;
        while (Forward(text, query, pos, text.Length, options) is var idx and >= 0)
        {
            found.Add(idx);
            pos = idx + query.Length;
        }

        return found;
    }

    /// <summary>Replaces every match; returns the new text and how many were replaced.</summary>
    public static (string Text, int Count) ReplaceAll(string text, string query, string replacement, SearchOptions options)
    {
        List<int> matches = Matches(text, query, options);
        if (matches.Count == 0)
        {
            return (text, 0);
        }

        var sb = new System.Text.StringBuilder(text.Length);
        int pos = 0;
        foreach (int idx in matches)
        {
            sb.Append(text, pos, idx - pos).Append(replacement);
            pos = idx + query.Length;
        }

        sb.Append(text, pos, text.Length - pos);
        return (sb.ToString(), matches.Count);
    }

    public static bool IsMatchAt(string text, string query, int index, int length, SearchOptions options)
    {
        if (length != query.Length || index < 0 || index + length > text.Length)
        {
            return false;
        }

        return string.Compare(text, index, query, 0, length, Comparison(options)) == 0
            && (!options.WholeWord || IsWholeWord(text, index, length));
    }

    private static StringComparison Comparison(SearchOptions options) =>
        options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    private static int Forward(string text, string query, int from, int to, SearchOptions options)
    {
        int pos = Math.Clamp(from, 0, text.Length);
        while (pos <= to - query.Length)
        {
            int idx = text.IndexOf(query, pos, to - pos, Comparison(options));
            if (idx < 0)
            {
                return -1;
            }

            if (!options.WholeWord || IsWholeWord(text, idx, query.Length))
            {
                return idx;
            }

            pos = idx + 1;
        }

        return -1;
    }

    private static int Backward(string text, string query, int endLimit, SearchOptions options)
    {
        int end = Math.Clamp(endLimit, 0, text.Length);
        while (end >= query.Length)
        {
            int idx = text.LastIndexOf(query, end - 1, end, Comparison(options));
            if (idx < 0)
            {
                return -1;
            }

            if (!options.WholeWord || IsWholeWord(text, idx, query.Length))
            {
                return idx;
            }

            end = idx + query.Length - 1;
        }

        return -1;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static bool IsWholeWord(string text, int index, int length)
    {
        bool startOk = index == 0 || !IsWordChar(text[index - 1]);
        int end = index + length;
        bool endOk = end >= text.Length || !IsWordChar(text[end]);
        return startOk && endOk;
    }
}
