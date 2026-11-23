using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace PadSharp.Tests;

public sealed class MainWindowSessionTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    private static void Close(Window window)
    {
        window.Close();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Click(Window owner, string label)
    {
        Dispatcher.UIThread.RunJobs();
        Window dialog = Assert.Single(owner.OwnedWindows);
        dialog.All<Button>().Single(b => b.Content as string == label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private string?[] SavedLanguages()
    {
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(sandbox.Data, "session.json")));
        return [.. json.RootElement.GetProperty("Tabs").EnumerateArray().Select(t => t.GetProperty("Language").GetString())];
    }

    [AvaloniaFact]
    public void UnsavedTextSurvivesClosingWithoutAQuestion()
    {
        MainWindow first = sandbox.Open();
        first.Current!.Editor.Document.Text = "draft";
        Close(first);

        Assert.Empty(first.OwnedWindows);
        MainWindow second = sandbox.Open();

        Document doc = Assert.Single(second.Documents);
        Assert.Equal(("draft", "Untitled-1", true), (doc.Text, doc.Title, doc.IsDirty));
    }

    [AvaloniaFact]
    public void FileTabsComeBackFromDiskWithTheCaretAndSelection()
    {
        string a = sandbox.File("a.txt", "alpha");
        string b = sandbox.File("b.txt", "bravo");
        MainWindow first = sandbox.Open(files: [a, b]);
        first.Documents[0].Editor.CaretOffset = 3;
        first.All<TabControl>().Single().SelectedItem = first.Documents[0].Tab;
        Close(first);

        MainWindow second = sandbox.Open();

        Assert.Equal(["a.txt", "b.txt"], second.Documents.Select(d => d.Title));
        Assert.Equal(("a.txt", 3, false), (second.Current?.Title, second.Current?.Editor.CaretOffset, second.Current?.IsDirty));
    }

    [AvaloniaFact]
    public void OnlyAChosenLanguageIsRemembered()
    {
        string rs = sandbox.File("m.rs", "fn main() {}");
        string txt = sandbox.File("n.txt", "print()");
        MainWindow first = sandbox.Open(files: [rs, txt]);
        first.Documents[1].Language = Languages.All.Single(l => l.Name == "Python");
        Close(first);

        Assert.Equal(new string?[] { null, "Python" }, SavedLanguages().AsEnumerable());
        MainWindow second = sandbox.Open();
        Assert.Equal(["Rust", "Python"], second.Documents.Select(d => d.Language.Name));
    }

    [AvaloniaFact]
    public void FilesThatAreGoneAreSkipped()
    {
        string path = sandbox.File("gone.txt", "x");
        Close(sandbox.Open(files: [path]));
        File.Delete(path);

        MainWindow second = sandbox.Open();

        Assert.Equal(["Untitled-1"], second.Documents.Select(d => d.Title));
    }

    [AvaloniaFact]
    public void ASecondWindowDoesNotOwnTheSessionAndAsksOnClose()
    {
        sandbox.Open();
        MainWindow second = sandbox.Open();
        second.Current!.Editor.Document.Text = "unsaved";

        second.Close();
        Dispatcher.UIThread.RunJobs();
        Assert.True(second.IsVisible);

        Click(second, "Don't Save");
        Assert.False(second.IsVisible);
    }

    [AvaloniaFact]
    public async Task AChangeMadeWhileClosedIsStillNoticed()
    {
        string path = sandbox.File("c.txt", "v1");
        MainWindow first = sandbox.Open(files: [path]);
        first.Current!.Editor.Document.Text = "mine";
        Close(first);
        File.WriteAllText(path, "theirs");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        // Opening activates the window, which checks the disk and asks straight away.
        MainWindow second = sandbox.Open();
        Assert.Equal("mine", second.Current!.Text);
        Click(second, "Reload");
        await second.CheckDiskChangesAsync();

        Assert.Equal(("theirs", false), (second.Current.Text, second.Current.IsDirty));
    }

    [AvaloniaFact]
    public void ACrashSaveKeepsUnsavedTextWhileTheWindowIsOpen()
    {
        MainWindow window = sandbox.Open();
        window.Current!.Editor.Document.Text = "almost lost";

        window.SaveSessionBeforeCrash();

        SessionTab tab = Assert.Single(Session.Load()!.Tabs);
        Assert.Equal("almost lost", Session.ReadBuffer(tab.Buffer!));
    }

    [AvaloniaFact]
    public void AFailedSessionSaveIsShownUntilOneWorks()
    {
        MainWindow window = sandbox.Open();
        window.Current!.Editor.Document.Text = "precious";
        string blocker = Path.Combine(sandbox.Root, "blocked");
        File.WriteAllText(blocker, "file");

        AppSettings.DataFolder = blocker;
        window.SaveSessionBeforeCrash();
        Assert.StartsWith("⚠ Unsaved text not protected", window.StatusParts[0], StringComparison.Ordinal);

        AppSettings.DataFolder = sandbox.Data;
        window.SaveSessionBeforeCrash();
        Assert.Equal("Plain Text", window.StatusParts[0]);
        Assert.Equal("precious", Session.ReadBuffer(Session.Load()!.Tabs.Single().Buffer!));
    }

    [AvaloniaFact]
    public async Task ASavedTabReloadsSilentlyWhenTheFileChanges()
    {
        string path = sandbox.File("w.txt", "v1");
        MainWindow window = sandbox.Open(files: [path]);
        File.WriteAllText(path, "v2");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        await window.CheckDiskChangesAsync();

        Assert.Equal(("v2", false), (window.Current!.Text, window.Current.IsDirty));
    }

    [AvaloniaFact]
    public async Task ATabWithChangesAsksAndCanKeepThem()
    {
        string path = sandbox.File("w.txt", "v1");
        MainWindow window = sandbox.Open(files: [path]);
        window.Current!.Editor.Document.Text = "mine";
        File.WriteAllText(path, "theirs");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Task check = window.CheckDiskChangesAsync();
        Click(window, "Keep Mine");
        await check;

        Assert.Equal(("mine", false), (window.Current.Text, window.Current.IsChangedOnDisk));
        await window.CheckDiskChangesAsync();
        Assert.Empty(window.OwnedWindows);
    }

    [AvaloniaFact]
    public async Task ATabWithChangesCanReload()
    {
        string path = sandbox.File("w.txt", "v1");
        MainWindow window = sandbox.Open(files: [path]);
        window.Current!.Editor.Document.Text = "mine";
        File.WriteAllText(path, "theirs");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        Task check = window.CheckDiskChangesAsync();
        Click(window, "Reload");
        await check;

        Assert.Equal(("theirs", false), (window.Current.Text, window.Current.IsDirty));
    }
}
