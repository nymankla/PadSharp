using System.Text.RegularExpressions;

namespace PadSharp;

/// <summary>How to search: case, whole words, direction, and whether the query is a regular expression.</summary>
internal readonly record struct SearchOptions(bool MatchCase, bool WholeWord, bool Backward, bool Regex = false);

internal readonly record struct SearchHit(int Index, int Length);

/// <summary>
/// Text search used by the find bar. Searches always wrap around the document. With <see cref="SearchOptions.Regex"/>
/// the query is a .NET regular expression: <c>^</c> and <c>$</c> match at line breaks, empty matches are skipped,
/// replacements may use <c>$1</c> / <c>${name}</c>, and a pattern that runs too long stops after a second.
/// </summary>
internal static class TextSearch
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static (string Pattern, RegexOptions Options, Regex Regex)? cached;

    /// <summary>Why the query can't be searched (an invalid regular expression), or null when it can.</summary>
    public static string? Problem(string query, SearchOptions options)
    {
        if (!options.Regex || query.Length == 0)
        {
            return null;
        }

        try
        {
            _ = RegexFor(query, options);
            return null;
        }
        catch (ArgumentException)
        {
            return "Invalid regular expression";
        }
    }

    /// <summary>
    /// Finds the next match, wrapping at the ends. Forward searches start at <paramref name="selEnd"/>;
    /// backward searches look for a match ending at or before <paramref name="selStart"/>.
    /// </summary>
    public static SearchHit? Find(string text, string query, int selStart, int selEnd, SearchOptions options)
    {
        List<SearchHit> hits = Hits(text, query, options);
        if (hits.Count == 0)
        {
            return null;
        }

        return options.Backward
            ? hits.LastOrDefault(h => h.Index + h.Length <= selStart, hits[^1])
            : hits.FirstOrDefault(h => h.Index >= selEnd, hits[0]);
    }

    /// <summary>Start index of every match, in order.</summary>
    public static List<int> Matches(string text, string query, SearchOptions options) =>
        [.. Hits(text, query, options).Select(h => h.Index)];

    /// <summary>Replaces every match; returns the new text and how many were replaced.</summary>
    public static (string Text, int Count) ReplaceAll(string text, string query, string replacement, SearchOptions options)
    {
        List<SearchHit> hits = Hits(text, query, options);
        if (hits.Count == 0)
        {
            return (text, 0);
        }

        var sb = new System.Text.StringBuilder(text.Length);
        int pos = 0;
        foreach (SearchHit hit in hits)
        {
            sb.Append(text, pos, hit.Index - pos).Append(Expand(text, query, hit, replacement, options));
            pos = hit.Index + hit.Length;
        }

        sb.Append(text, pos, text.Length - pos);
        return (sb.ToString(), hits.Count);
    }

    /// <summary>True when exactly this span is a match (the find bar's Replace checks the selection with it).</summary>
    public static bool IsMatchAt(string text, string query, int index, int length, SearchOptions options) =>
        ReplacementAt(text, query, index, length, string.Empty, options) is not null;

    /// <summary>
    /// The text that replaces the match at exactly this span (with <c>$1</c> etc. filled in for a regular
    /// expression), or null when the span isn't a match.
    /// </summary>
    public static string? ReplacementAt(string text, string query, int index, int length, string replacement, SearchOptions options)
    {
        if (length == 0 || index < 0 || index + length > text.Length)
        {
            return null;
        }

        var hit = new SearchHit(index, length);
        return Hits(text, query, options).Contains(hit) ? Expand(text, query, hit, replacement, options) : null;
    }

    /// <summary>Every match in order, without overlaps; empty for an empty or invalid query.</summary>
    private static List<SearchHit> Hits(string text, string query, SearchOptions options)
    {
        if (query.Length == 0 || Problem(query, options) is not null)
        {
            return [];
        }

        return options.Regex ? RegexHits(text, RegexFor(query, options), options) : PlainHits(text, query, options);
    }

    private static List<SearchHit> PlainHits(string text, string query, SearchOptions options)
    {
        var hits = new List<SearchHit>();
        StringComparison comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int pos = 0;
        while (pos <= text.Length - query.Length && text.IndexOf(query, pos, comparison) is var idx and >= 0)
        {
            bool accepted = !options.WholeWord || IsWholeWord(text, idx, query.Length);
            if (accepted)
            {
                hits.Add(new SearchHit(idx, query.Length));
            }

            pos = accepted ? idx + query.Length : idx + 1;
        }

        return hits;
    }

    private static List<SearchHit> RegexHits(string text, Regex regex, SearchOptions options)
    {
        var hits = new List<SearchHit>();
        try
        {
            for (Match m = regex.Match(text); m.Success; m = m.NextMatch())
            {
                if (m.Length > 0 && (!options.WholeWord || IsWholeWord(text, m.Index, m.Length)))
                {
                    hits.Add(new SearchHit(m.Index, m.Length));
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // A runaway pattern: keep what was found rather than hang the editor.
        }

        return hits;
    }

    /// <summary>The replacement for one hit: as typed, or with groups filled in for a regular expression.</summary>
    private static string Expand(string text, string query, SearchHit hit, string replacement, SearchOptions options)
    {
        if (!options.Regex)
        {
            return replacement;
        }

        Match m = RegexFor(query, options).Match(text, hit.Index);
        return m.Success && m.Index == hit.Index ? m.Result(replacement) : replacement;
    }

    /// <summary>The compiled pattern; the last one is reused, since the find bar asks again on every key.</summary>
    private static Regex RegexFor(string pattern, SearchOptions options)
    {
        RegexOptions flags = RegexOptions.Multiline | RegexOptions.CultureInvariant
            | (options.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        if (cached is { } c && c.Pattern == pattern && c.Options == flags)
        {
            return c.Regex;
        }

        var regex = new Regex(pattern, flags, RegexTimeout);
        cached = (pattern, flags, regex);
        return regex;
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
