using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;

namespace PadSharp.Tests;

public sealed class MainWindowMenuTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    [AvaloniaFact]
    public void CtrlTabCyclesThroughTabs()
    {
        MainWindow window = sandbox.Open();
        window.Press(Key.N, RawInputModifiers.Control);

        window.Press(Key.Tab, RawInputModifiers.Control);
        Assert.Equal("Untitled-1", window.Current?.Title);

        window.Press(Key.PageUp, RawInputModifiers.Control);
        Assert.Equal("Untitled-2", window.Current?.Title);
    }

    [AvaloniaFact]
    public void TheLanguageMenuIsOneFlatList()
    {
        MainWindow window = sandbox.Open();
        MenuItem menu = window.All<MenuItem>().Single(m => m.Header as string == "_Language");

        Assert.Equal(Languages.All.Count, menu.Items.Count);
        Assert.All(menu.Items.OfType<MenuItem>(), item => Assert.Empty(item.Items));

        window.RunMenu("Rust");
        Assert.Equal("Rust", window.Current!.Language.Name);
    }

    [AvaloniaFact]
    public void ShortcutsUseTheCommandKeyOfThePlatform()
    {
        var save = KeyGesture.Parse("Ctrl+S");
        var tabs = KeyGesture.Parse("Ctrl+Shift+W");
        var f3 = KeyGesture.Parse("Shift+F3");

        Assert.Same(save, MainWindow.ForPlatform(save, KeyModifiers.Control));
        Assert.Equal(new KeyGesture(Key.S, KeyModifiers.Meta), MainWindow.ForPlatform(save, KeyModifiers.Meta));
        Assert.Equal(new KeyGesture(Key.W, KeyModifiers.Meta | KeyModifiers.Shift), MainWindow.ForPlatform(tabs, KeyModifiers.Meta));
        Assert.Same(f3, MainWindow.ForPlatform(f3, KeyModifiers.Meta));
    }
}
