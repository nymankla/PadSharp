using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class FindBarTests : IDisposable
{
    private readonly FindBar bar = new();
    private readonly Window window;
    private readonly List<FindRequestEventArgs> requests = [];

    public FindBarTests()
    {
        window = new Window { Content = bar };
        window.Show();
        bar.FindRequested += (_, e) => requests.Add(e);
    }

    public void Dispose() => window.Close();

    private TextBox Box(string placeholder) => bar.All<TextBox>().Single(t => t.PlaceholderText == placeholder);

    private List<FindAction> Actions => [.. requests.Select(r => r.Action)];

    [AvaloniaFact]
    public void StartsHiddenAndOpensSeededWithTheSelection()
    {
        Assert.False(bar.IsVisible);

        bar.Open(replace: false, seed: "needle");
        Dispatcher.UIThread.RunJobs();

        Assert.True(bar.IsVisible);
        Assert.Equal("needle", bar.Query);
        Assert.False(Box("Replace").IsEffectivelyVisible);
        Assert.Equal(FindAction.Incremental, Assert.Single(requests).Action);
    }

    [AvaloniaFact]
    public void MultiLineSelectionsAreNotUsedAsTheQuery()
    {
        bar.Open(replace: false, seed: "kept");
        bar.Open(replace: true, seed: "two\nlines");

        Assert.Equal("kept", bar.Query);
        Assert.True(Box("Replace").IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void EnterFindsNextAndShiftEnterPrevious()
    {
        bar.Open(replace: false, seed: "x");
        Dispatcher.UIThread.RunJobs();
        requests.Clear();

        window.Press(Key.Enter);
        window.Press(Key.Enter, RawInputModifiers.Shift);

        Assert.Equal([FindAction.Next, FindAction.Previous], Actions);
    }

    [AvaloniaFact]
    public void TypingSearchesIncrementally()
    {
        bar.Open(replace: false, seed: string.Empty);
        Dispatcher.UIThread.RunJobs();
        requests.Clear();

        window.Type("ab");

        Assert.Equal("ab", bar.Query);
        Assert.All(requests, r => Assert.Equal(FindAction.Incremental, r.Action));
        Assert.Equal("ab", requests[^1].Query);
    }

    [AvaloniaFact]
    public void EnterInTheReplaceBoxReplacesAndCtrlAltEnterReplacesAll()
    {
        bar.Open(replace: true, seed: "a");
        Box("Replace").Text = "b";
        Box("Replace").Focus();
        Dispatcher.UIThread.RunJobs();
        requests.Clear();

        window.Press(Key.Enter);
        window.Press(Key.Enter, RawInputModifiers.Control | RawInputModifiers.Alt);

        Assert.Equal([FindAction.Replace, FindAction.ReplaceAll], Actions);
        Assert.All(requests, r => Assert.Equal(("a", "b"), (r.Query, r.Replacement)));
    }

    [AvaloniaFact]
    public void TogglesSetTheOptionsAndSearchAgain()
    {
        bar.Open(replace: false, seed: "x");
        Dispatcher.UIThread.RunJobs();
        requests.Clear();
        ToggleButton[] toggles = [.. bar.All<ToggleButton>()];

        toggles.Single(t => t.Content as string == "Aa").IsChecked = true;
        toggles.Single(t => t.Content as string == "W").IsChecked = true;
        toggles.Single(t => t.Content as string == ".*").IsChecked = true;

        Assert.Equal(new SearchOptions(MatchCase: true, WholeWord: true, Backward: false, Regex: true), bar.Options);
        Assert.Equal([FindAction.Incremental, FindAction.Incremental, FindAction.Incremental], Actions);
    }

    [AvaloniaFact]
    public void ButtonsRaiseTheirActions()
    {
        bar.Open(replace: true, seed: "x");
        Dispatcher.UIThread.RunJobs();
        requests.Clear();

        foreach (string label in new[] { "↓", "↑", "Replace", "All" })
        {
            bar.All<Button>().Single(b => b.Content as string == label)
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        }

        Assert.Equal([FindAction.Next, FindAction.Previous, FindAction.Replace, FindAction.ReplaceAll], Actions);
    }

    [AvaloniaFact]
    public void EscapeAndTheCloseButtonHideIt()
    {
        int closed = 0;
        bar.Closed += (_, _) => closed++;

        bar.Open(replace: false, seed: "x");
        window.Press(Key.Escape);
        Assert.False(bar.IsVisible);

        bar.Open(replace: false, seed: "x");
        bar.All<Button>().Single(b => b.Content as string == "✕").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.False(bar.IsVisible);
        Assert.Equal(2, closed);
    }

    [AvaloniaFact]
    public void StatusShowsProblemsInTheProblemColour()
    {
        bar.SetStatus("No results", problem: true);
        TextBlock status = bar.All<TextBlock>().Single(t => t.Text == "No results");
        Assert.Same(Look.ProblemText, status.Foreground);

        bar.SetStatus("1 of 2", problem: false);
        Assert.Same(Look.MutedFore, status.Foreground);
        Assert.IsAssignableFrom<IBrush>(status.Foreground);
    }
}
