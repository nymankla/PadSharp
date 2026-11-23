using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace PadSharp.Tests;

public sealed class MainWindowFindTests : IDisposable
{
    private readonly Sandbox sandbox = new();
    private readonly MainWindow window;
    private readonly Document doc;

    public MainWindowFindTests()
    {
        window = sandbox.Open();
        doc = window.Current!;
        doc.Editor.Document.Text = "cat Cat dog cat";
        doc.Editor.CaretOffset = 0;
    }

    public void Dispose() => sandbox.Dispose();

    private string Selected => doc.Editor.SelectedText;

    private string FindStatus => window.FindBar.All<TextBlock>().Single(t => t.MinWidth == 90).Text ?? string.Empty;

    private void Search(string query)
    {
        window.Press(Key.F, RawInputModifiers.Control);
        window.Type(query);
    }

    [AvaloniaFact]
    public void CtrlFOpensTheBarAndTypingFindsTheFirstMatch()
    {
        Search("cat");

        Assert.True(window.FindBar.IsVisible);
        Assert.Equal((0, "cat"), (doc.Editor.SelectionStart, Selected));
        Assert.Equal("1 of 3", FindStatus);
    }

    [AvaloniaFact]
    public void EnterAndShiftEnterStepThroughMatchesAndWrap()
    {
        Search("cat");

        window.Press(Key.Enter);
        Assert.Equal((4, "2 of 3"), (doc.Editor.SelectionStart, FindStatus));
        window.Press(Key.Enter);
        window.Press(Key.Enter);
        Assert.Equal((0, "1 of 3"), (doc.Editor.SelectionStart, FindStatus));

        window.Press(Key.Enter, RawInputModifiers.Shift);
        Assert.Equal(12, doc.Editor.SelectionStart);
    }

    [AvaloniaFact]
    public void MatchCaseNarrowsTheResults()
    {
        Search("cat");
        window.FindBar.All<ToggleButton>().Single(t => t.Content as string == "Aa").IsChecked = true;

        Assert.Equal("1 of 2", FindStatus);
    }

    private void UseRegex() =>
        window.FindBar.All<ToggleButton>().Single(t => t.Content as string == ".*").IsChecked = true;

    [AvaloniaFact]
    public void RegexSearchSelectsTheWholeMatch()
    {
        Search("d.g|C.t");
        UseRegex();

        Assert.Equal("1 of 4", FindStatus);
        Assert.Equal("cat", Selected);
        window.Press(Key.Enter);
        window.Press(Key.Enter);
        Assert.Equal((8, "dog", "3 of 4"), (doc.Editor.SelectionStart, Selected, FindStatus));
    }

    [AvaloniaFact]
    public void AnInvalidRegexIsReported()
    {
        UseRegex();
        Search("(");

        Assert.Equal("Invalid regular expression", FindStatus);

        window.Press(Key.Escape);
        window.Press(Key.F3);
        Assert.Equal("Invalid regular expression", window.StatusParts[0]);
    }

    [AvaloniaFact]
    public void RegexReplaceUsesGroups()
    {
        window.Press(Key.H, RawInputModifiers.Control);
        UseRegex();
        window.FindBar.All<TextBox>().Single(t => t.PlaceholderText == "Find").Focus();
        window.Type(@"(\w+) (\w+)");
        TextBox replace = window.FindBar.All<TextBox>().Single(t => t.PlaceholderText == "Replace");
        replace.Text = "$2 $1";
        replace.Focus();

        window.Press(Key.Enter);
        Assert.Equal("Cat cat dog cat", doc.Text);

        window.Press(Key.Enter, RawInputModifiers.Control | RawInputModifiers.Alt);
        Assert.Equal("cat Cat cat dog", doc.Text);
        Assert.Equal("Replaced 2", FindStatus);
    }

    [AvaloniaFact]
    public void NoMatchSaysSo()
    {
        Search("bird");

        Assert.Equal("No results", FindStatus);
        Assert.Equal(string.Empty, Selected);
    }

    [AvaloniaFact]
    public void TheSelectionSeedsTheBar()
    {
        doc.Editor.Select(8, 3);

        window.Press(Key.F, RawInputModifiers.Control);

        Assert.Equal("dog", window.FindBar.Query);
    }

    [AvaloniaFact]
    public void EscapeClosesTheBarAndF3KeepsSearchingInTheStatusBar()
    {
        Search("cat");
        window.Press(Key.Escape);
        Assert.False(window.FindBar.IsVisible);

        window.Press(Key.F3);

        Assert.Equal(4, doc.Editor.SelectionStart);
        Assert.Equal("2 of 3", window.StatusParts[0]);

        window.Press(Key.F3, RawInputModifiers.Shift);
        Assert.Equal(0, doc.Editor.SelectionStart);
    }

    [AvaloniaFact]
    public void F3WithoutAQueryOpensTheBar()
    {
        window.Press(Key.F3);

        Assert.True(window.FindBar.IsVisible);
    }

    [AvaloniaFact]
    public void ReplaceReplacesTheCurrentMatchAndMovesOn()
    {
        window.Press(Key.H, RawInputModifiers.Control);
        window.Type("cat");
        TextBox replace = window.FindBar.All<TextBox>().Single(t => t.PlaceholderText == "Replace");
        replace.Text = "cow";
        replace.Focus();

        window.Press(Key.Enter);

        Assert.Equal("cow Cat dog cat", doc.Text);
        Assert.Equal((4, "Cat"), (doc.Editor.SelectionStart, Selected));
    }

    [AvaloniaFact]
    public void ReplaceAllIsOneUndoStep()
    {
        window.Press(Key.H, RawInputModifiers.Control);
        window.Type("cat");
        TextBox replace = window.FindBar.All<TextBox>().Single(t => t.PlaceholderText == "Replace");
        replace.Text = "x";
        replace.Focus();

        window.Press(Key.Enter, RawInputModifiers.Control | RawInputModifiers.Alt);

        Assert.Equal("x x dog x", doc.Text);
        Assert.Equal("Replaced 3", FindStatus);
        doc.Editor.Undo();
        Assert.Equal("cat Cat dog cat", doc.Text);
    }
}
