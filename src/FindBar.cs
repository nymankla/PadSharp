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

internal sealed class FindRequestEventArgs(FindAction action, string query, string replacement, SearchOptions options) : EventArgs
{
    public FindAction Action { get; } = action;

    public string Query { get; } = query;

    public string Replacement { get; } = replacement;

    public SearchOptions Options { get; } = options;
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

        Button previous = Small("↑", "Previous match (Shift+Enter)", () => Raise(FindAction.Previous));
        Button next = Small("↓", "Next match (Enter)", () => Raise(FindAction.Next));
        Button close = Small("✕", "Close (Esc)", Hide);
        Button replace = Small("Replace", "Replace (Enter)", () => Raise(FindAction.Replace));
        Button replaceAll = Small("All", "Replace all (Ctrl+Alt+Enter)", () => Raise(FindAction.ReplaceAll));

        var findRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { findBox, matchCase, wholeWord, regex, previous, next, statusText },
        };
        replaceRow.Children.AddRange([replaceBox, replace, replaceAll]);

        var rows = new StackPanel { Spacing = 4, Children = { findRow, replaceRow } };
        var dock = new DockPanel();
        DockPanel.SetDock(close, Dock.Right);
        close.VerticalAlignment = VerticalAlignment.Top;
        dock.Children.AddRange([close, rows]);
        Child = dock;

        findBox.TextChanged += (_, _) => Raise(FindAction.Incremental);
        matchCase.IsCheckedChanged += (_, _) => Raise(FindAction.Incremental);
        wholeWord.IsCheckedChanged += (_, _) => Raise(FindAction.Incremental);
        regex.IsCheckedChanged += (_, _) => Raise(FindAction.Incremental);
        findBox.KeyDown += (_, e) => OnBoxKey(e, e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? FindAction.Previous : FindAction.Next);
        replaceBox.KeyDown += (_, e) => OnBoxKey(e, e.KeyModifiers.HasFlag(KeyModifiers.Control | KeyModifiers.Alt) ? FindAction.ReplaceAll : FindAction.Replace);
    }

    public event EventHandler<FindRequestEventArgs>? FindRequested;

    /// <summary>Raised when the bar closes, so the editor can take focus back.</summary>
    public event EventHandler? Closed;

    public string Query => findBox.Text ?? string.Empty;

    public SearchOptions Options =>
        new(matchCase.IsChecked == true, wholeWord.IsChecked == true, Backward: false, Regex: regex.IsChecked == true);

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
        FindRequested?.Invoke(this, new FindRequestEventArgs(action, Query, replaceBox.Text ?? string.Empty, Options));
}
