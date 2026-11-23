using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace PadSharp;

/// <summary>The text editor's styles, compiled from EditorStyles.axaml.</summary>
internal sealed partial class EditorStyles : Styles
{
    public EditorStyles() => AvaloniaXamlLoader.Load(this);
}
