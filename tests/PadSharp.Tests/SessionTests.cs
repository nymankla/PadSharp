using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class SessionTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    private string Buffers => Path.Combine(sandbox.Data, "buffers");

    [AvaloniaFact]
    public void NothingIsLoadedBeforeTheFirstSave() => Assert.Null(Session.Load());

    [AvaloniaFact]
    public void SavedTabsAndBuffersLoadBack()
    {
        var state = new SessionState
        {
            Selected = 1,
            Tabs =
            [
                new SessionTab { Path = @"C:\a.txt", Eol = EolKind.Unix, Encoding = TextEncodingKind.Utf8Bom, Caret = 7 },
                new SessionTab { UntitledNumber = 2, Buffer = "b1.txt", Language = "Rust" },
            ],
        };

        Session.Save(state, [("b1.txt", "unsaved text")]);
        SessionState? loaded = Session.Load();

        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.Selected);
        Assert.Equal(2, loaded.Tabs.Count);
        Assert.Equal((@"C:\a.txt", EolKind.Unix, TextEncodingKind.Utf8Bom, 7, (string?)null),
            (loaded.Tabs[0].Path, loaded.Tabs[0].Eol, loaded.Tabs[0].Encoding, loaded.Tabs[0].Caret, loaded.Tabs[0].Buffer));
        Assert.Equal((2, "b1.txt", "Rust"), (loaded.Tabs[1].UntitledNumber, loaded.Tabs[1].Buffer, loaded.Tabs[1].Language));
        Assert.Equal("unsaved text", Session.ReadBuffer("b1.txt"));
    }

    [AvaloniaFact]
    public void BuffersNoTabNamesAreDeleted()
    {
        Session.Save(new SessionState { Tabs = [new SessionTab { Buffer = "keep.txt" }, new SessionTab { Buffer = "drop.txt" }] },
            [("keep.txt", "k"), ("drop.txt", "d")]);
        Session.Save(new SessionState { Tabs = [new SessionTab { Buffer = "keep.txt" }] }, []);

        Assert.Equal(["keep.txt"], Directory.EnumerateFiles(Buffers).Select(Path.GetFileName));
        Assert.Equal("k", Session.ReadBuffer("keep.txt"));
        Assert.Null(Session.ReadBuffer("drop.txt"));
    }

    [AvaloniaFact]
    public void ReadBufferOnlyReadsFromTheBuffersFolder()
    {
        Directory.CreateDirectory(sandbox.Data);
        File.WriteAllText(Path.Combine(sandbox.Data, "secret.txt"), "outside");

        Assert.Null(Session.ReadBuffer(Path.Combine("..", "secret.txt")));
    }

    [AvaloniaFact]
    public void ADamagedStateFileLoadsAsNothing()
    {
        Directory.CreateDirectory(sandbox.Data);
        File.WriteAllText(Path.Combine(sandbox.Data, "session.json"), "[[[");

        Assert.Null(Session.Load());
    }

    [AvaloniaFact]
    public void SavingLeavesNoHalfWrittenFilesAndKeepsTheDiskStamp()
    {
        DateTime stamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        string? problem = Session.Save(new SessionState { Tabs = [new SessionTab { Buffer = "b.txt", DiskStamp = stamp }] }, [("b.txt", "x")]);

        Assert.Null(problem);
        Assert.Empty(Directory.EnumerateFiles(sandbox.Data, "*.tmp", SearchOption.AllDirectories));
        Assert.Equal(stamp, Session.Load()?.Tabs.Single().DiskStamp);
    }

    [AvaloniaFact]
    public void AFailedSaveSaysWhy()
    {
        string blocker = Path.Combine(sandbox.Root, "not-a-folder");
        File.WriteAllText(blocker, "a file where the data folder should be");
        AppSettings.DataFolder = blocker;

        Assert.NotNull(Session.Save(new SessionState(), [("b.txt", "x")]));
    }

    [AvaloniaFact]
    public void OwnershipGoesToOneWindowPerClaim()
    {
        Assert.True(Session.TryClaim());
        Assert.True(Session.TryTakeOwnership());
        Assert.False(Session.TryTakeOwnership());

        Session.Release();
        Assert.True(Session.TryTakeOwnership());
    }

    [AvaloniaFact]
    public void OnlyOneOwnerAtATime()
    {
        Assert.True(Session.TryClaim());
        Assert.False(Session.TryClaim());

        Session.Release();

        Assert.True(Session.TryClaim());
        Session.Release();
    }
}
