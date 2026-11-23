using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class DialogsTests : IDisposable
{
    private readonly Window owner = new();

    public DialogsTests() => owner.Show();

    public void Dispose() => owner.Close();

    private Window Dialog()
    {
        Dispatcher.UIThread.RunJobs();
        return Assert.Single(owner.OwnedWindows);
    }

    private static void Click(Window dialog, string label) =>
        dialog.All<Button>().Single(b => b.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [AvaloniaFact]
    public async Task AskReturnsTheButtonPressed()
    {
        Task<string?> answer = Dialogs.AskAsync(owner, "Title", "Message?", "Yes", "No");
        Window dialog = Dialog();

        Assert.Equal("Title", dialog.Title);
        Assert.Contains(dialog.All<TextBlock>(), t => t.Text == "Message?");
        Assert.Contains("accent", dialog.All<Button>().First().Classes);
        Click(dialog, "No");

        Assert.Equal("No", await answer);
    }

    [AvaloniaFact]
    public async Task EscapeCancelsAQuestion()
    {
        Task<string?> answer = Dialogs.AskAsync(owner, "Title", "Message?", "OK");
        Dialog().Press(Key.Escape);

        Assert.Null(await answer);
    }

    [AvaloniaFact]
    public async Task GoToLineReturnsTheChosenLine()
    {
        Task<int?> line = Dialogs.GoToLineAsync(owner, currentLine: 2, lastLine: 40);
        Window dialog = Dialog();
        NumericUpDown input = dialog.All<NumericUpDown>().Single();

        Assert.Equal((1m, 40m, 2m), (input.Minimum, input.Maximum, input.Value));
        Assert.Contains(dialog.All<TextBlock>(), t => t.Text == "Line (1-40):");
        input.Value = 17;
        Click(dialog, "Go");

        Assert.Equal(17, await line);
    }

    [AvaloniaFact]
    public async Task GoToLineCanBeCancelled()
    {
        Task<int?> line = Dialogs.GoToLineAsync(owner, currentLine: 9, lastLine: 3);
        Window dialog = Dialog();

        Assert.Equal(3m, dialog.All<NumericUpDown>().Single().Value);
        Click(dialog, "Cancel");

        Assert.Null(await line);
    }
}
