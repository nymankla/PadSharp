using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace PadSharp;

internal enum FindAction
{
    /// <summary>The query changed: find from the start of the current selection.</summary>
    Incremental,
    Next,
    Previous,
    Replace,
    ReplaceAll,
}

internal sealed class FindRequestEventArgs(FindAction action, SearchQuery search, string replacement) : EventArgs
{
    public FindAction Action { get; } = action;

    /// <summary>The query with its options (case, whole word, regex), as set in the bar.</summary>
    public SearchQuery Search { get; } = search;

    public string Query => Search.Text;

    public string Replacement { get; } = replacement;
}

/// <summary>The find / replace bar docked under the editor. Enter finds next, Shift+Enter previous, Esc closes.</summary>
internal sealed class FindBar : Border
{
    private readonly TextBox findBox = new() { PlaceholderText = "Find", MinWidth = 260 };
    private readonly TextBox replaceBox = new() { PlaceholderText = "Replace", MinWidth = 260 };
    private readonly ToggleButton matchCase = Toggle("Aa", "Match case");
    private readonly ToggleButton wholeWord = Toggle("W", "Whole word");
    private readonly ToggleButton regex = Toggle(".*", "Regular expression ($1 in Replace inserts a group)");
    private readonly TextBlock statusText = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 90, FontSize = 12 };
    private readonly StackPanel replaceRow = new() { Orientation = Orientation.Horizontal, Spacing = 4 };

    public FindBar()
    {
        Background = Look.PanelBack;
        BorderBrush = Look.ChromeLine;
        BorderThickness = new Thickness(0, 1, 0, 0);
        Padding = new Thickness(8, 6);
        IsVisible = false;
        Child = BuildLayout();

        findBox.TextChanged += (_, _) => Raise(FindAction.Incremental);
        matchCase.IsCheckedChanged += (_, _) => Raise(FindAction.Incremental);
        wholeWord.IsCheckedChanged += (_, _) => Raise(FindAction.Incremental);
        regex.IsCheckedChanged += (_, _) => Raise(FindAction.Incremental);
        findBox.KeyDown += (_, e) => OnBoxKey(e, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? FindAction.Previous : FindAction.Next);
        replaceBox.KeyDown += (_, e) => OnBoxKey(e, e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Alt) ? FindAction.ReplaceAll : FindAction.Replace);
    }

    /// <summary>Find row (box, toggles, previous/next, status) over the replace row, with a close button on the right.</summary>
    private DockPanel BuildLayout()
    {
        var findRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children =
            {
                findBox, matchCase, wholeWord, regex,
                Small("↑", "Previous match (Shift+Enter)", () => Raise(FindAction.Previous)),
                Small("↓", "Next match (Enter)", () => Raise(FindAction.Next)),
                statusText,
            },
        };
        replaceRow.Children.AddRange([
            replaceBox,
            Small("Replace", "Replace (Enter)", () => Raise(FindAction.Replace)),
            Small("All", "Replace all (Ctrl+Alt+Enter)", () => Raise(FindAction.ReplaceAll))]);

        Button close = Small("✕", "Close (Esc)", Hide);
        close.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(close, Dock.Right);
        var rows = new StackPanel { Spacing = 4, Children = { findRow, replaceRow } };
        return new DockPanel { Children = { close, rows } };
    }

    public event EventHandler<FindRequestEventArgs>? FindRequested;

    /// <summary>Raised when the bar closes, so the editor can take focus back.</summary>
    public event EventHandler? Closed;

    public string Query => findBox.Text ?? string.Empty;

    /// <summary>The query and the options as set in the bar (always forward; F3 and Shift+Enter set the direction).</summary>
    public SearchQuery Search =>
        new(Query, matchCase.IsChecked == true, wholeWord.IsChecked == true, Backward: false, Regex: regex.IsChecked == true);

    /// <summary>Shows the bar (with the replace row when asked), seeded with a single-line selection.</summary>
    public void Open(bool replace, string seed)
    {
        replaceRow.IsVisible = replace;
        IsVisible = true;
        if (seed.Length > 0 && !seed.Contains('\n', StringComparison.Ordinal))
        {
            findBox.Text = seed;
        }

        findBox.Focus();
        findBox.SelectAll();
    }

    public void Hide()
    {
        IsVisible = false;
        statusText.Text = string.Empty;
        Closed?.Invoke(this, EventArgs.Empty);
    }

    public void SetStatus(string message, bool problem)
    {
        statusText.Text = message;
        statusText.Foreground = problem ? Look.ProblemText : Look.MutedFore;
    }

    private static ToggleButton Toggle(string text, string tip)
    {
        var button = new ToggleButton { Content = text, Padding = new Thickness(8, 4), FontWeight = FontWeight.SemiBold, Focusable = false };
        ToolTip.SetTip(button, tip);
        return button;
    }

    private static Button Small(string text, string tip, Action run)
    {
        var button = new Button { Content = text, Padding = new Thickness(8, 4), Focusable = false };
        button.Click += (_, _) => run();
        ToolTip.SetTip(button, tip);
        return button;
    }

    private void OnBoxKey(KeyEventArgs e, FindAction onEnter)
    {
        if (e.Key == Key.Enter)
        {
            Raise(onEnter);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
    }

    private void Raise(FindAction action) =>
        FindRequested?.Invoke(this, new FindRequestEventArgs(action, Search, replaceBox.Text ?? string.Empty));
}
