using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace PadSharp.Tests;

public sealed class MainWindowFilesTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void FilesFromTheCommandLineReplaceTheEmptyTab()
    {
        string a = sandbox.File("a.txt", "A");
        string b = sandbox.File("b.md", "B");

        MainWindow window = sandbox.Open(files: [a, b, a]);

        Assert.Equal(["a.txt", "b.md"], window.TabTitles());
        Assert.Equal("a.txt", window.Current?.Title);
    }

    [AvaloniaFact]
    public void AMissingFileIsReported()
    {
        MainWindow window = sandbox.Open(files: [Path.Combine(sandbox.Files, "gone.txt")]);

        Window dialog = window.Dialog();
        Assert.Contains(dialog.All<TextBlock>(), t => t.Text?.Contains("doesn't exist", StringComparison.Ordinal) == true);
        dialog.Click("OK");
        Assert.Equal(["Untitled-1"], window.TabTitles());
    }

    [AvaloniaFact]
    public void NewTabsTakeTheSmallestFreeNumber()
    {
        MainWindow window = sandbox.Open();
        window.Press(Key.N, RawInputModifiers.Control);
        window.Press(Key.N, RawInputModifiers.Control);
        Assert.Equal(["Untitled-1", "Untitled-2", "Untitled-3"], window.TabTitles());

        window.SelectTab(window.Documents[1]);
        window.Press(Key.W, RawInputModifiers.Control);
        window.Press(Key.N, RawInputModifiers.Control);

        Assert.Equal(["Untitled-1", "Untitled-3", "Untitled-2"], window.TabTitles());
    }

    [AvaloniaFact]
    public void ClosingTheLastTabLeavesAFreshOne()
    {
        MainWindow window = sandbox.Open();
        Document first = window.Documents[0];

        window.Press(Key.W, RawInputModifiers.Control);

        Assert.NotSame(first, Assert.Single(window.Documents));
        Assert.Equal(["Untitled-1"], window.TabTitles());
    }

    [AvaloniaFact]
    public void ClosingAChangedTabAsksFirst()
    {
        MainWindow window = sandbox.Open();
        window.Documents[0].Editor.Document.Insert(0, "keep me");

        window.Press(Key.W, RawInputModifiers.Control);
        Window ask = window.Dialog();
        Assert.Contains(ask.All<TextBlock>(), t => t.Text == "Save changes to \"Untitled-1\"?");
        ask.Click("Cancel");
        Assert.Equal("keep me", window.Documents[0].Text);

        window.Press(Key.W, RawInputModifiers.Control);
        window.Dialog().Click("Don't Save");
        Assert.Equal(string.Empty, Assert.Single(window.Documents).Text);
    }

    [AvaloniaFact]
    public void SaveWritesAFileTab()
    {
        string path = sandbox.File("s.txt", "old");
        MainWindow window = sandbox.Open(files: [path]);
        window.Current!.Editor.Document.Text = "new";

        window.Press(Key.S, RawInputModifiers.Control);

        Assert.Equal("new", File.ReadAllText(path));
        Assert.False(window.Current.IsDirty);
    }

    [AvaloniaFact]
    public void ReloadReadsTheFileAgain()
    {
        string path = sandbox.File("r.txt", "one");
        MainWindow window = sandbox.Open(files: [path]);
        File.WriteAllText(path, "two");

        window.RunMenu("_Reload from Disk");

        Assert.Equal("two", window.Current!.Text);
    }

    [AvaloniaFact]
    public void ReloadAsksBeforeThrowingAwayChanges()
    {
        string path = sandbox.File("r.txt", "disk");
        MainWindow window = sandbox.Open(files: [path]);
        window.Current!.Editor.Document.Text = "mine";

        window.RunMenu("_Reload from Disk");
        window.Dialog().Click("Cancel");
        Assert.Equal("mine", window.Current.Text);

        window.RunMenu("_Reload from Disk");
        window.Dialog().Click("Reload");
        Assert.Equal("disk", window.Current.Text);
    }
}
