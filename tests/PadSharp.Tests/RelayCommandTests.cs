using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class RelayCommandTests
{
    [AvaloniaFact]
    public void ExecuteRunsTheAction()
    {
        int runs = 0;
        var command = new RelayCommand(() => runs++);

        command.Execute(null);
        command.Execute("ignored");

        Assert.Equal(2, runs);
    }

    [AvaloniaFact]
    public void CanAlwaysExecute()
    {
        var command = new RelayCommand(() => { });
        EventHandler handler = (_, _) => { };
        command.CanExecuteChanged += handler;
        command.CanExecuteChanged -= handler;

        Assert.True(command.CanExecute(null));
    }
}
