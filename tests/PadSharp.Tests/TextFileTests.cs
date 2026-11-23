using Avalonia.Headless.XUnit;

namespace PadSharp.Tests;

public sealed class TextFileTests : IDisposable
{
    private readonly Sandbox sandbox = new();

    public void Dispose() => sandbox.Dispose();

    private string Bytes(params byte[] bytes)
    {
        string path = Path.Combine(sandbox.Files, Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [AvaloniaFact]
    public void LoadsUtf8AndNormalisesLineBreaks()
    {
        LoadedText loaded = TextFile.Load(Bytes("a\r\nb\r\nc"u8.ToArray()));

        Assert.Equal("a\nb\nc", loaded.Text);
        Assert.Equal(TextEncodingKind.Utf8, loaded.Encoding);
        Assert.Equal(EolKind.Windows, loaded.Eol);
    }

    [AvaloniaFact]
    public void HonoursByteOrderMarks()
    {
        Assert.Equal(TextEncodingKind.Utf8Bom, TextFile.Load(Bytes(0xEF, 0xBB, 0xBF, (byte)'a')).Encoding);

        LoadedText little = TextFile.Load(Bytes(0xFF, 0xFE, (byte)'h', 0, (byte)'i', 0));
        Assert.Equal((TextEncodingKind.Utf16LeBom, "hi"), (little.Encoding, little.Text));

        LoadedText big = TextFile.Load(Bytes(0xFE, 0xFF, 0, (byte)'h', 0, (byte)'i'));
        Assert.Equal((TextEncodingKind.Utf16BeBom, "hi"), (big.Encoding, big.Text));
    }

    [AvaloniaFact]
    public void ReadsInvalidUtf8AsAnsi()
    {
        LoadedText loaded = TextFile.Load(Bytes((byte)'c', (byte)'a', (byte)'f', 0xE9));

        Assert.Equal(TextEncodingKind.Ansi, loaded.Encoding);
        Assert.StartsWith("caf", loaded.Text, StringComparison.Ordinal);
        Assert.Equal(4, loaded.Text.Length);
    }

    [AvaloniaTheory]
    [InlineData("a\rb", (int)EolKind.Macintosh)]
    [InlineData("a\nb", (int)EolKind.Unix)]
    [InlineData("a\r\nb\nc", (int)EolKind.Windows)]
    [InlineData("no line break", (int)EolKind.Windows)]
    public void TheFirstLineBreakDecidesTheStyle(string text, int expected) =>
        Assert.Equal((EolKind)expected, TextFile.Load(sandbox.File(Guid.NewGuid() + ".txt", text)).Eol);

    [AvaloniaFact]
    public void SaveWritesTheLineBreaksAndPreamble()
    {
        string path = Path.Combine(sandbox.Files, "out.txt");

        TextFile.Save(path, "a\nb", TextEncodingKind.Utf8Bom, EolKind.Windows);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a', 13, 10, (byte)'b' }, File.ReadAllBytes(path));

        TextFile.Save(path, "a\nb", TextEncodingKind.Utf16LeBom, EolKind.Unix);
        Assert.Equal(new byte[] { 0xFF, 0xFE, (byte)'a', 0, 10, 0, (byte)'b', 0 }, File.ReadAllBytes(path));
    }

    [AvaloniaTheory]
    [InlineData((int)TextEncodingKind.Utf8, (int)EolKind.Windows)]
    [InlineData((int)TextEncodingKind.Utf8Bom, (int)EolKind.Unix)]
    [InlineData((int)TextEncodingKind.Utf16LeBom, (int)EolKind.Macintosh)]
    [InlineData((int)TextEncodingKind.Utf16BeBom, (int)EolKind.Windows)]
    [InlineData((int)TextEncodingKind.Ansi, (int)EolKind.Unix)]
    public void SavedFilesLoadBackUnchanged(int encodingKind, int eolKind)
    {
        var encoding = (TextEncodingKind)encodingKind;
        var eol = (EolKind)eolKind;
        string path = Path.Combine(sandbox.Files, "round.txt");
        // Non-ASCII on purpose: ASCII-only ANSI is also valid UTF-8 and (correctly) loads back as UTF-8.
        const string text = "café\nline two\n";

        TextFile.Save(path, text, encoding, eol);
        LoadedText loaded = TextFile.Load(path);

        Assert.Equal((text, encoding, eol), (loaded.Text, loaded.Encoding, loaded.Eol));
    }

    [AvaloniaFact]
    public void NormalizeEolTurnsEveryBreakIntoLf() =>
        Assert.Equal("a\nb\nc\n", TextFile.NormalizeEol("a\r\nb\rc\n"));

    [AvaloniaFact]
    public void NamesAndBreakTexts()
    {
        Assert.Equal(("Windows (CRLF)", "CRLF", "\r\n"), (TextFile.EolName(EolKind.Windows), TextFile.EolShortName(EolKind.Windows), TextFile.EolText(EolKind.Windows)));
        Assert.Equal(("Unix (LF)", "LF", "\n"), (TextFile.EolName(EolKind.Unix), TextFile.EolShortName(EolKind.Unix), TextFile.EolText(EolKind.Unix)));
        Assert.Equal(("Classic Mac (CR)", "CR", "\r"), (TextFile.EolName(EolKind.Macintosh), TextFile.EolShortName(EolKind.Macintosh), TextFile.EolText(EolKind.Macintosh)));
        Assert.Equal(
            ["ANSI", "UTF-8", "UTF-8 BOM", "UTF-16 BE", "UTF-16 LE"],
            Enum.GetValues<TextEncodingKind>().Select(TextFile.EncodingName));
    }
}
