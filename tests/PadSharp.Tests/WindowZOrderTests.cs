using Avalonia.Controls;
using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class WindowZOrderTests
{
    [AvaloniaFact]
    public void SendToBackIsHarmlessWithoutANativeWindow()
    {
        var window = new Window();
        Assert.Null(Record.Exception(() => WindowZOrder.SendToBack(window)));

        window.Show();
        Assert.Null(Record.Exception(() => WindowZOrder.SendToBack(window)));
        window.Close();
    }
}
