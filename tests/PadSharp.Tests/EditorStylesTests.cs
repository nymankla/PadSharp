using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class EditorStylesTests
{
    [AvaloniaFact]
    public void TheCompiledXamlLoadsItsStyles() => Assert.NotEmpty(new EditorStyles());
}
