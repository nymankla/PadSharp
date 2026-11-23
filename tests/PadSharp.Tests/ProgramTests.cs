using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class ProgramTests
{
    [AvaloniaFact]
    public void TheAppBuilderStartsTheEditorApp() =>
        Assert.Equal(typeof(App), Program.BuildAvaloniaApp().ApplicationType);
}
