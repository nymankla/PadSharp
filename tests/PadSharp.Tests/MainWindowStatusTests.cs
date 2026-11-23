using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class MainWindowStatusTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void ShowsLanguagePositionLinesLineEndingsAndEncoding()
    {
        MainWindow window = sandbox.Open(files: [sandbox.File("c.yaml", "a: 1\r\nb: 2\r\n")]);

        Assert.Equal(["YAML", "Ln 1, Col 1", "3 lines", "CRLF", "UTF-8"], window.StatusParts);
    }

    [AvaloniaFact]
    public void FollowsTheCaretAndTheSelection()
    {
        MainWindow window = sandbox.Open();
        Document doc = window.Current!;
        doc.Editor.Document.Text = "first\n\tsecond";

        doc.Editor.CaretOffset = 8;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Ln 2, Col 6", window.StatusParts[1]);
        Assert.Equal("2 lines", window.StatusParts[2]);

        doc.Editor.Select(0, 5);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Ln 1, Col 6  (5 selected)", window.StatusParts[1]);
    }

    [AvaloniaFact]
    public void InsertTogglesOvertype()
    {
        MainWindow window = sandbox.Open();
        window.Current!.Editor.TextArea.Focus();

        window.Press(Key.Insert);
        Assert.EndsWith("OVR", window.StatusParts[1], StringComparison.Ordinal);

        window.Press(Key.Insert);
        Assert.DoesNotContain("OVR", window.StatusParts[1], StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheTitleMarksUnsavedChanges()
    {
        MainWindow window = sandbox.Open();

        window.Current!.Editor.Document.Insert(0, "x");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("*Untitled-1 - PadSharp", window.Title);
    }

    [AvaloniaFact]
    public void MenuChecksFollowTheTabInFront()
    {
        MainWindow window = sandbox.Open(files: [sandbox.File("x.rs", "fn main() {}\n")]);
        bool Checked(string header) => window.All<MenuItem>().Single(m => m.Header as string == header).IsChecked;

        Assert.True(Checked("Rust"));
        Assert.True(Checked("Unix (LF)"));
        Assert.True(Checked("UTF-8"));
        Assert.False(Checked("Python"));

        window.Press(Key.N, RawInputModifiers.Control);

        Assert.True(Checked("Plain Text"));
        Assert.True(Checked("Windows (CRLF)"));
        Assert.Equal("Plain Text", window.StatusParts[0]);
    }
}
