using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace PadSharp;

/// <summary>Small modal dialogs: a message with buttons, and Go To line.</summary>
internal static class Dialogs
{
    /// <summary>Shows a message and returns the label of the button pressed (null if closed).</summary>
    public static Task<string?> AskAsync(Window owner, string title, string message, params string[] buttons)
    {
        Window dialog = NewDialog(title);
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 18,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 420 },
                ButtonRow(dialog, buttons),
            },
        };
        dialog.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                dialog.Close(null);
            }
        };
        return dialog.ShowDialog<string?>(owner);
    }

    /// <summary>Asks for a line number; null when cancelled.</summary>
    public static Task<int?> GoToLineAsync(Window owner, int currentLine, int lastLine)
    {
        Window dialog = NewDialog("Go to Line");
        int last = Math.Max(1, lastLine);
        var input = new NumericUpDown { Minimum = 1, Maximum = last, Value = Math.Clamp(currentLine, 1, last), FormatString = "0", Width = 140 };
        var go = new Button { Content = "Go", MinWidth = 80, IsDefault = true, Classes = { "accent" } };
        var cancel = new Button { Content = "Cancel", MinWidth = 80, IsCancel = true };
        go.Click += (_, _) => dialog.Close((int?)(int)(input.Value ?? 1));
        cancel.Click += (_, _) => dialog.Close(null);

        var label = new TextBlock { Text = $"Line (1-{last.ToString(CultureInfo.CurrentCulture)}):", VerticalAlignment = VerticalAlignment.Center };
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(18),
            Spacing = 14,
            Children =
            {
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { label, input } },
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { go, cancel } },
            },
        };
        dialog.Opened += (_, _) => input.Focus();
        return dialog.ShowDialog<int?>(owner);
    }

    /// <summary>One button per label, right-aligned; the first is the accent (default) one. Each closes with its label.</summary>
    private static StackPanel ButtonRow(Window dialog, string[] labels)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        foreach (string label in labels)
        {
            var button = new Button { Content = label, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
            button.Click += (_, _) => dialog.Close(label);
            row.Children.Add(button);
        }

        if (row.Children.FirstOrDefault() is Button first)
        {
            first.Classes.Add("accent");
        }

        return row;
    }

    private static Window NewDialog(string title) => new()
    {
        Title = title,
        SizeToContent = SizeToContent.WidthAndHeight,
        CanResize = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        MinWidth = 300,
        Background = Look.PanelBack,
    };
}
