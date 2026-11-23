using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class TextSearchTests
{
    private static readonly SearchOptions Plain = new(MatchCase: false, WholeWord: false, Backward: false);

    [AvaloniaFact]
    public void FindsForwardFromTheSelectionEnd() =>
        Assert.Equal(new SearchHit(3, 2), TextSearch.Find("ab ab ab", "ab", 0, 2, Plain));

    [AvaloniaFact]
    public void WrapsToTheTopWhenNothingFollows() =>
        Assert.Equal(0, TextSearch.Find("ab ab", "ab", 3, 5, Plain)?.Index);

    [AvaloniaFact]
    public void FindsBackwardBeforeTheSelectionStart() =>
        Assert.Equal(3, TextSearch.Find("ab ab ab", "ab", 6, 8, Plain with { Backward = true })?.Index);

    [AvaloniaFact]
    public void WrapsToTheBottomGoingBackward() =>
        Assert.Equal(6, TextSearch.Find("ab ab ab", "ab", 0, 2, Plain with { Backward = true })?.Index);

    [AvaloniaFact]
    public void ReturnsNullWithoutAMatchOrAQuery()
    {
        Assert.Null(TextSearch.Find("abc", "x", 0, 0, Plain));
        Assert.Null(TextSearch.Find("abc", "x", 0, 0, Plain with { Backward = true }));
        Assert.Null(TextSearch.Find("abc", string.Empty, 0, 0, Plain));
    }

    [AvaloniaFact]
    public void MatchCaseIsOptional()
    {
        Assert.Equal(0, TextSearch.Find("ABC", "abc", 0, 0, Plain)?.Index);
        Assert.Null(TextSearch.Find("ABC", "abc", 0, 0, Plain with { MatchCase = true }));
    }

    [AvaloniaFact]
    public void WholeWordSkipsMatchesInsideWords()
    {
        const string text = "cat_x xcat cat";
        Assert.Equal([0, 7, 11], TextSearch.Matches(text, "cat", Plain));
        Assert.Equal([11], TextSearch.Matches(text, "cat", Plain with { WholeWord = true }));
        Assert.Equal(11, TextSearch.Find(text, "cat", 0, 0, Plain with { WholeWord = true })?.Index);
        Assert.Equal(11, TextSearch.Find(text, "cat", 14, 14, Plain with { WholeWord = true, Backward = true })?.Index);
    }

    [AvaloniaFact]
    public void MatchesDoNotOverlap()
    {
        Assert.Equal([0, 2], TextSearch.Matches("aaaa", "aa", Plain));
        Assert.Empty(TextSearch.Matches("aaaa", string.Empty, Plain));
    }

    [AvaloniaFact]
    public void ReplaceAllReplacesEveryMatchAndCountsThem()
    {
        Assert.Equal(("x-b-x", 2), TextSearch.ReplaceAll("a-b-a", "a", "x", Plain));
        Assert.Equal(("a-b-a", 0), TextSearch.ReplaceAll("a-b-a", "z", "x", Plain));
        Assert.Equal(("a-b-a", 0), TextSearch.ReplaceAll("a-b-a", string.Empty, "x", Plain));
    }

    private static readonly SearchOptions Rx = Plain with { Regex = true };

    [AvaloniaFact]
    public void RegexMatchesHaveTheirOwnLength()
    {
        Assert.Equal(new SearchHit(4, 3), TextSearch.Find("id: 123 and 45", @"\d+", 0, 0, Rx));
        Assert.Equal([4, 12], TextSearch.Matches("id: 123 and 45", @"\d+", Rx));
        Assert.Equal(new SearchHit(12, 2), TextSearch.Find("id: 123 and 45", @"\d+", 4, 7, Rx));
    }

    [AvaloniaFact]
    public void RegexFollowsMatchCaseAndAnchorsAtLineBreaks()
    {
        Assert.Equal([0, 4], TextSearch.Matches("Cat\ncat", "^cat$", Rx));
        Assert.Equal([4], TextSearch.Matches("Cat\ncat", "^cat$", Rx with { MatchCase = true }));
    }

    [AvaloniaFact]
    public void RegexSearchesBackwardAndWraps()
    {
        Assert.Equal(0, TextSearch.Find("a1 b2 c3", "[a-z][0-9]", 3, 5, Rx with { Backward = true })?.Index);
        Assert.Equal(6, TextSearch.Find("a1 b2 c3", "[a-z][0-9]", 0, 2, Rx with { Backward = true })?.Index);
    }

    [AvaloniaFact]
    public void RegexWholeWordAndEmptyMatchesAreSkipped()
    {
        Assert.Equal([5], TextSearch.Matches("cats cat", "cat", Rx with { WholeWord = true }));
        Assert.Equal([1], TextSearch.Matches("ba", "a*", Rx));
    }

    [AvaloniaFact]
    public void RegexReplacementsFillInGroups()
    {
        Assert.Equal(("y=1, x=2", 2), TextSearch.ReplaceAll("1=y, 2=x", @"(\d)=(\w)", "$2=$1", Rx));
        Assert.Equal("<abc>", TextSearch.ReplacementAt("x abc y", @"(?<w>[a-c]+)", 2, 3, "<${w}>", Rx));
        Assert.Null(TextSearch.ReplacementAt("x abc y", "[a-c]+", 2, 2, "-", Rx));
        Assert.Equal("$1", TextSearch.ReplacementAt("a", "a", 0, 1, "$1", Plain));
    }

    [AvaloniaFact]
    public void AnInvalidPatternIsReportedNotThrown()
    {
        Assert.Equal("Invalid regular expression", TextSearch.Problem("(", Rx));
        Assert.Null(TextSearch.Problem("(", Plain));
        Assert.Null(TextSearch.Problem("a+", Rx));
        Assert.Null(TextSearch.Find("(((", "(", 0, 0, Rx));
        Assert.Equal(("(((", 0), TextSearch.ReplaceAll("(((", "(", "x", Rx));
        Assert.Equal(0, TextSearch.Find("(((", "(", 0, 0, Plain)?.Index);
    }

    [AvaloniaFact]
    public void ARunawayPatternStopsInsteadOfHanging()
    {
        string text = new string('a', 40) + "!";
        var timer = System.Diagnostics.Stopwatch.StartNew();

        Assert.Empty(TextSearch.Matches(text, "(a+)+$", Rx));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
    }

    [AvaloniaFact]
    public void IsMatchAtChecksTheExactSpan()
    {
        Assert.True(TextSearch.IsMatchAt("say Hello", "hello", 4, 5, Plain));
        Assert.False(TextSearch.IsMatchAt("say Hello", "hello", 4, 5, Plain with { MatchCase = true }));
        Assert.False(TextSearch.IsMatchAt("say Hello", "hello", 4, 4, Plain));
        Assert.False(TextSearch.IsMatchAt("say Hello", "hello", 6, 5, Plain));
        Assert.False(TextSearch.IsMatchAt("say Hello", "hello", -1, 5, Plain));
        Assert.False(TextSearch.IsMatchAt("Helloo", "hello", 0, 5, Plain with { WholeWord = true }));
    }
}
