using System.Windows.Input;

namespace PadSharp;

/// <summary>A command that runs an action; menus use it.</summary>
internal sealed class RelayCommand(Action run) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => run();
}
