using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class LineNumberGutterTests
{
    private static double WidthFor(Document doc, int lines)
    {
        doc.Editor.Document.Text = string.Join("\n", Enumerable.Repeat("x", lines));
        doc.Gutter.Measure(Size.Infinity);
        return doc.Gutter.DesiredSize.Width;
    }

    [AvaloniaFact]
    public void GrowsWithTheNumberOfDigits()
    {
        var doc = new Document(1);

        double two = WidthFor(doc, 5);
        Assert.Equal(two, WidthFor(doc, 99));
        Assert.True(WidthFor(doc, 1000) > two);
    }

    [AvaloniaFact]
    public void FollowsTheFontSize()
    {
        var doc = new Document(1);
        double small = WidthFor(doc, 10);

        doc.Gutter.NumberFontSize = 40;

        Assert.True(WidthFor(doc, 10) > small);
    }

    [AvaloniaFact]
    public void TracksTheCaretLineAndRenders()
    {
        var doc = new Document(1);
        doc.Editor.Document.Text = "a\nb\nc";
        var window = new Window { Content = doc.Editor, Width = 400, Height = 300 };
        window.Show();

        doc.Editor.TextArea.Caret.Line = 3;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, doc.Gutter.CurrentLine);
        Assert.NotNull(window.CaptureRenderedFrame());
        window.Close();
    }
}
