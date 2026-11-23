using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace PadSharp.Tests;

public sealed class MainWindowEditTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void LineCommandsDuplicateAndDelete()
    {
        MainWindow window = sandbox.Open();
        Document doc = window.Current!;
        doc.Editor.Document.Text = "one\ntwo";
        doc.Editor.CaretOffset = 0;

        window.Press(Key.D, RawInputModifiers.Control);
        Assert.Equal("one\none\ntwo", doc.Text);

        window.Press(Key.L, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("one\ntwo", doc.Text);

        doc.Editor.CaretOffset = doc.Text.Length;
        window.Press(Key.L, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("one", doc.Text);
    }

    [AvaloniaFact]
    public void CaseCommandsConvertTheSelection()
    {
        MainWindow window = sandbox.Open();
        Document doc = window.Current!;
        doc.Editor.Document.Text = "say hello";
        doc.Editor.Select(4, 5);

        window.Press(Key.U, RawInputModifiers.Control | RawInputModifiers.Shift);
        Assert.Equal("say HELLO", doc.Text);

        window.Press(Key.U, RawInputModifiers.Control);
        Assert.Equal("say hello", doc.Text);
    }

    [AvaloniaFact]
    public void LineEndingAndEncodingChoicesMarkTheTabChanged()
    {
        MainWindow window = sandbox.Open();

        window.RunMenu("Unix (LF)");
        window.RunMenu("UTF-16 LE");

        Document doc = window.Current!;
        Assert.Equal((EolKind.Unix, TextEncodingKind.Utf16LeBom, true), (doc.Eol, doc.Encoding, doc.IsDirty));
    }

    [AvaloniaFact]
    public void GoToLineMovesTheCaret()
    {
        MainWindow window = sandbox.Open();
        window.Current!.Editor.Document.Text = "1\n2\n3\n4";

        window.Press(Key.G, RawInputModifiers.Control);
        Window dialog = window.Dialog();
        dialog.All<NumericUpDown>().Single().Value = 3;
        dialog.Click("Go");

        Assert.Equal(3, window.Current.Editor.TextArea.Caret.Line);
    }
}
