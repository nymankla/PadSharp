using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class TextSearchTests
{
    private static SearchQuery Plain(string text) => new(text);

    private static SearchQuery Rx(string pattern) => new(pattern, Regex: true);

    [AvaloniaFact]
    public void FindsForwardFromTheSelectionEnd() =>
        Assert.Equal(new SearchHit(3, 2), TextSearch.Find("ab ab ab", Plain("ab"), 0, 2));

    [AvaloniaFact]
    public void WrapsToTheTopWhenNothingFollows() =>
        Assert.Equal(0, TextSearch.Find("ab ab", Plain("ab"), 3, 5)?.Index);

    [AvaloniaFact]
    public void FindsBackwardBeforeTheSelectionStart() =>
        Assert.Equal(3, TextSearch.Find("ab ab ab", Plain("ab") with { Backward = true }, 6, 8)?.Index);

    [AvaloniaFact]
    public void WrapsToTheBottomGoingBackward() =>
        Assert.Equal(6, TextSearch.Find("ab ab ab", Plain("ab") with { Backward = true }, 0, 2)?.Index);

    [AvaloniaFact]
    public void ReturnsNullWithoutAMatchOrAQuery()
    {
        Assert.Null(TextSearch.Find("abc", Plain("x"), 0, 0));
        Assert.Null(TextSearch.Find("abc", Plain("x") with { Backward = true }, 0, 0));
        Assert.Null(TextSearch.Find("abc", Plain(string.Empty), 0, 0));
        Assert.Null(TextSearch.Find("abc", default, 0, 0));
    }

    [AvaloniaFact]
    public void MatchCaseIsOptional()
    {
        Assert.Equal(0, TextSearch.Find("ABC", Plain("abc"), 0, 0)?.Index);
        Assert.Null(TextSearch.Find("ABC", Plain("abc") with { MatchCase = true }, 0, 0));
    }

    [AvaloniaFact]
    public void WholeWordSkipsMatchesInsideWords()
    {
        const string text = "cat_x xcat cat";
        SearchQuery whole = Plain("cat") with { WholeWord = true };
        Assert.Equal([0, 7, 11], TextSearch.Matches(text, Plain("cat")));
        Assert.Equal([11], TextSearch.Matches(text, whole));
        Assert.Equal(11, TextSearch.Find(text, whole, 0, 0)?.Index);
        Assert.Equal(11, TextSearch.Find(text, whole with { Backward = true }, 14, 14)?.Index);
    }

    [AvaloniaFact]
    public void MatchesDoNotOverlap()
    {
        Assert.Equal([0, 2], TextSearch.Matches("aaaa", Plain("aa")));
        Assert.Empty(TextSearch.Matches("aaaa", Plain(string.Empty)));
    }

    [AvaloniaFact]
    public void ReplaceAllReplacesEveryMatchAndCountsThem()
    {
        Assert.Equal(("x-b-x", 2), TextSearch.ReplaceAll("a-b-a", Plain("a"), "x"));
        Assert.Equal(("a-b-a", 0), TextSearch.ReplaceAll("a-b-a", Plain("z"), "x"));
        Assert.Equal(("a-b-a", 0), TextSearch.ReplaceAll("a-b-a", Plain(string.Empty), "x"));
    }

    [AvaloniaFact]
    public void RegexMatchesHaveTheirOwnLength()
    {
        Assert.Equal(new SearchHit(4, 3), TextSearch.Find("id: 123 and 45", Rx(@"\d+"), 0, 0));
        Assert.Equal([4, 12], TextSearch.Matches("id: 123 and 45", Rx(@"\d+")));
        Assert.Equal(new SearchHit(12, 2), TextSearch.Find("id: 123 and 45", Rx(@"\d+"), 4, 7));
    }

    [AvaloniaFact]
    public void RegexFollowsMatchCaseAndAnchorsAtLineBreaks()
    {
        Assert.Equal([0, 4], TextSearch.Matches("Cat\ncat", Rx("^cat$")));
        Assert.Equal([4], TextSearch.Matches("Cat\ncat", Rx("^cat$") with { MatchCase = true }));
    }

    [AvaloniaFact]
    public void RegexSearchesBackwardAndWraps()
    {
        SearchQuery back = Rx("[a-z][0-9]") with { Backward = true };
        Assert.Equal(0, TextSearch.Find("a1 b2 c3", back, 3, 5)?.Index);
        Assert.Equal(6, TextSearch.Find("a1 b2 c3", back, 0, 2)?.Index);
    }

    [AvaloniaFact]
    public void RegexWholeWordAndEmptyMatchesAreSkipped()
    {
        Assert.Equal([5], TextSearch.Matches("cats cat", Rx("cat") with { WholeWord = true }));
        Assert.Equal([1], TextSearch.Matches("ba", Rx("a*")));
    }

    [AvaloniaFact]
    public void RegexReplacementsFillInGroups()
    {
        Assert.Equal(("y=1, x=2", 2), TextSearch.ReplaceAll("1=y, 2=x", Rx(@"(\d)=(\w)"), "$2=$1"));
        Assert.Equal("<abc>", TextSearch.ReplacementAt("x abc y", Rx(@"(?<w>[a-c]+)"), new SearchHit(2, 3), "<${w}>"));
        Assert.Null(TextSearch.ReplacementAt("x abc y", Rx("[a-c]+"), new SearchHit(2, 2), "-"));
        Assert.Equal("$1", TextSearch.ReplacementAt("a", Plain("a"), new SearchHit(0, 1), "$1"));
    }

    [AvaloniaFact]
    public void AnInvalidPatternIsReportedNotThrown()
    {
        Assert.Equal("Invalid regular expression", TextSearch.Problem(Rx("(")));
        Assert.Null(TextSearch.Problem(Plain("(")));
        Assert.Null(TextSearch.Problem(Rx("a+")));
        Assert.Null(TextSearch.Find("(((", Rx("("), 0, 0));
        Assert.Equal(("(((", 0), TextSearch.ReplaceAll("(((", Rx("("), "x"));
        Assert.Equal(0, TextSearch.Find("(((", Plain("("), 0, 0)?.Index);
    }

    [AvaloniaFact]
    public void ARunawayPatternStopsInsteadOfHanging()
    {
        string text = new string('a', 40) + "!";
        var timer = System.Diagnostics.Stopwatch.StartNew();

        Assert.Empty(TextSearch.Matches(text, Rx("(a+)+$")));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
    }

    [AvaloniaFact]
    public void IsMatchAtChecksTheExactSpan()
    {
        Assert.True(TextSearch.IsMatchAt("say Hello", Plain("hello"), new SearchHit(4, 5)));
        Assert.False(TextSearch.IsMatchAt("say Hello", Plain("hello") with { MatchCase = true }, new SearchHit(4, 5)));
        Assert.False(TextSearch.IsMatchAt("say Hello", Plain("hello"), new SearchHit(4, 4)));
        Assert.False(TextSearch.IsMatchAt("say Hello", Plain("hello"), new SearchHit(6, 5)));
        Assert.False(TextSearch.IsMatchAt("say Hello", Plain("hello"), new SearchHit(-1, 5)));
        Assert.False(TextSearch.IsMatchAt("Helloo", Plain("hello") with { WholeWord = true }, new SearchHit(0, 5)));
    }
}
