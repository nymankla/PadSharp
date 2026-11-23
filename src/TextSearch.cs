using System.Text.RegularExpressions;

namespace PadSharp;

/// <summary>What to search for and how: the text, case, whole words, direction, and whether it is a regular expression.</summary>
internal readonly record struct SearchQuery(string Text, bool MatchCase = false, bool WholeWord = false, bool Backward = false, bool Regex = false);

internal readonly record struct SearchHit(int Index, int Length);

/// <summary>
/// Text search used by the find bar. Searches always wrap around the document. With <see cref="SearchQuery.Regex"/>
/// the query is a .NET regular expression: <c>^</c> and <c>$</c> match at line breaks, empty matches are skipped,
/// replacements may use <c>$1</c> / <c>${name}</c>, and a pattern that runs too long stops after a second.
/// </summary>
internal static class TextSearch
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);
    private static (string Pattern, RegexOptions Options, Regex Regex)? cached;

    /// <summary>Why the query can't be searched (an invalid regular expression), or null when it can.</summary>
    public static string? Problem(SearchQuery query)
    {
        if (!query.Regex || string.IsNullOrEmpty(query.Text))
        {
            return null;
        }

        try
        {
            _ = RegexFor(query);
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
    public static SearchHit? Find(string text, SearchQuery query, int selStart, int selEnd)
    {
        List<SearchHit> hits = Hits(text, query);
        if (hits.Count == 0)
        {
            return null;
        }

        return query.Backward
            ? hits.LastOrDefault(h => h.Index + h.Length <= selStart, hits[^1])
            : hits.FirstOrDefault(h => h.Index >= selEnd, hits[0]);
    }

    /// <summary>Start index of every match, in order.</summary>
    public static List<int> Matches(string text, SearchQuery query) => [.. Hits(text, query).Select(h => h.Index)];

    /// <summary>Replaces every match; returns the new text and how many were replaced.</summary>
    public static (string Text, int Count) ReplaceAll(string text, SearchQuery query, string replacement)
    {
        List<SearchHit> hits = Hits(text, query);
        var sb = new System.Text.StringBuilder(text.Length);
        int pos = 0;
        foreach (SearchHit hit in hits)
        {
            sb.Append(text, pos, hit.Index - pos).Append(Expand(text, query, hit, replacement));
            pos = hit.Index + hit.Length;
        }

        sb.Append(text, pos, text.Length - pos);
        return (hits.Count == 0 ? text : sb.ToString(), hits.Count);
    }

    /// <summary>True when exactly this span is a match (the find bar's Replace checks the selection with it).</summary>
    public static bool IsMatchAt(string text, SearchQuery query, SearchHit span) =>
        ReplacementAt(text, query, span, string.Empty) is not null;

    /// <summary>
    /// The text that replaces the match at exactly this span (with <c>$1</c> etc. filled in for a regular
    /// expression), or null when the span isn't a match.
    /// </summary>
    public static string? ReplacementAt(string text, SearchQuery query, SearchHit span, string replacement)
    {
        bool inside = span.Length > 0 && span.Index >= 0 && span.Index + span.Length <= text.Length;
        return inside && Hits(text, query).Contains(span) ? Expand(text, query, span, replacement) : null;
    }

    /// <summary>Every match in order, without overlaps; empty for an empty or invalid query.</summary>
    private static List<SearchHit> Hits(string text, SearchQuery query)
    {
        if (string.IsNullOrEmpty(query.Text) || Problem(query) is not null)
        {
            return [];
        }

        return query.Regex ? RegexHits(text, query) : PlainHits(text, query);
    }

    private static List<SearchHit> PlainHits(string text, SearchQuery query)
    {
        var hits = new List<SearchHit>();
        StringComparison comparison = query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int length = query.Text.Length;
        int pos = 0;
        while (pos <= text.Length - length && text.IndexOf(query.Text, pos, comparison) is var idx and >= 0)
        {
            bool accepted = !query.WholeWord || IsWholeWord(text, idx, length);
            if (accepted)
            {
                hits.Add(new SearchHit(idx, length));
            }

            pos = accepted ? idx + length : idx + 1;
        }

        return hits;
    }

    private static List<SearchHit> RegexHits(string text, SearchQuery query)
    {
        var hits = new List<SearchHit>();
        try
        {
            for (Match m = RegexFor(query).Match(text); m.Success; m = m.NextMatch())
            {
                if (m.Length > 0 && (!query.WholeWord || IsWholeWord(text, m.Index, m.Length)))
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
    private static string Expand(string text, SearchQuery query, SearchHit hit, string replacement)
    {
        if (!query.Regex)
        {
            return replacement;
        }

        Match m = RegexFor(query).Match(text, hit.Index);
        return m.Success && m.Index == hit.Index ? m.Result(replacement) : replacement;
    }

    /// <summary>The compiled pattern; the last one is reused, since the find bar asks again on every key.</summary>
    private static Regex RegexFor(SearchQuery query)
    {
        RegexOptions flags = RegexOptions.Multiline | RegexOptions.CultureInvariant
            | (query.MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
        if (cached is { } c && c.Pattern == query.Text && c.Options == flags)
        {
            return c.Regex;
        }

        var regex = new Regex(query.Text, flags, RegexTimeout);
        cached = (query.Text, flags, regex);
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
