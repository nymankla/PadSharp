using System.Globalization;
using System.Text;

namespace PadSharp;

/// <summary>Line ending style, as shown in the status bar.</summary>
internal enum EolKind
{
    Windows = 0,
    Macintosh = 1,
    Unix = 2,
}

/// <summary>How a document is written to disk.</summary>
internal enum TextEncodingKind
{
    Ansi,
    Utf8,
    Utf8Bom,
    Utf16BeBom,
    Utf16LeBom,
}

/// <summary>A loaded file: text with LF breaks, its encoding, its line-ending style, and whether it mixed styles.</summary>
internal sealed record LoadedText(string Text, TextEncodingKind Encoding, EolKind Eol, bool MixedEol = false);

/// <summary>Reads and writes plain text files, detecting encoding and line endings.</summary>
internal static class TextFile
{
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static TextFile()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding AnsiEncoding =>
        Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

    /// <summary>Menu name, e.g. "Windows (CRLF)".</summary>
    public static string EolName(EolKind eol) => eol switch
    {
        EolKind.Macintosh => "Classic Mac (CR)",
        EolKind.Unix => "Unix (LF)",
        _ => "Windows (CRLF)",
    };

    /// <summary>Status-bar name: CRLF, LF or CR.</summary>
    public static string EolShortName(EolKind eol) => eol switch
    {
        EolKind.Macintosh => "CR",
        EolKind.Unix => "LF",
        _ => "CRLF",
    };

    public static string EolText(EolKind eol) => eol switch
    {
        EolKind.Macintosh => "\r",
        EolKind.Unix => "\n",
        _ => "\r\n",
    };

    public static string EncodingName(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Ansi => "ANSI",
        TextEncodingKind.Utf8 => "UTF-8",
        TextEncodingKind.Utf8Bom => "UTF-8 BOM",
        TextEncodingKind.Utf16BeBom => "UTF-16 BE",
        TextEncodingKind.Utf16LeBom => "UTF-16 LE",
        _ => "UTF-8",
    };

    public static LoadedText Load(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        (string raw, TextEncodingKind enc) = Decode(bytes);
        EolKind eol = DetectEol(raw);
        return new LoadedText(NormalizeEol(raw), enc, eol, HasMixedEol(raw));
    }

    public static void Save(string path, string normalizedText, TextEncodingKind encoding, EolKind eol)
    {
        string text = normalizedText.Replace("\n", EolText(eol), StringComparison.Ordinal);
        Encoding enc = GetEncoding(encoding);
        byte[] preamble = enc.GetPreamble();
        byte[] body = enc.GetBytes(text);
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        stream.Write(preamble, 0, preamble.Length);
        stream.Write(body, 0, body.Length);
    }

    /// <summary>Converts any CR LF / CR line breaks to LF, the editor's internal form.</summary>
    public static string NormalizeEol(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static Encoding GetEncoding(TextEncodingKind kind) => kind switch
    {
        TextEncodingKind.Ansi => AnsiEncoding,
        TextEncodingKind.Utf8Bom => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
        TextEncodingKind.Utf16LeBom => new UnicodeEncoding(bigEndian: false, byteOrderMark: true),
        TextEncodingKind.Utf16BeBom => new UnicodeEncoding(bigEndian: true, byteOrderMark: true),
        _ => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    };

    /// <summary>Byte-order marks and what they mean, checked in order.</summary>
    private static readonly (byte[] Bom, Encoding Encoding, TextEncodingKind Kind)[] Boms =
    [
        ([0xEF, 0xBB, 0xBF], Encoding.UTF8, TextEncodingKind.Utf8Bom),
        ([0xFF, 0xFE], Encoding.Unicode, TextEncodingKind.Utf16LeBom),
        ([0xFE, 0xFF], Encoding.BigEndianUnicode, TextEncodingKind.Utf16BeBom),
    ];

    /// <summary>A byte-order mark decides; otherwise valid UTF-8 is UTF-8 and anything else is ANSI.</summary>
    private static (string Text, TextEncodingKind Kind) Decode(byte[] bytes)
    {
        foreach ((byte[] bom, Encoding encoding, TextEncodingKind kind) in Boms)
        {
            if (bytes.AsSpan().StartsWith(bom))
            {
                return (encoding.GetString(bytes, bom.Length, bytes.Length - bom.Length), kind);
            }
        }

        try
        {
            return (StrictUtf8.GetString(bytes), TextEncodingKind.Utf8);
        }
        catch (DecoderFallbackException)
        {
            return (AnsiEncoding.GetString(bytes), TextEncodingKind.Ansi);
        }
    }

    /// <summary>True when the text uses more than one line-ending style (saving would convert some lines).</summary>
    public static bool HasMixedEol(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Breaks(text).Distinct().Skip(1).Any();
    }

    /// <summary>The first line break decides; a text without one is Windows (CRLF).</summary>
    private static EolKind DetectEol(string text) => Breaks(text).DefaultIfEmpty(EolKind.Windows).First();

    /// <summary>The style of every line break in the text, in order.</summary>
    private static IEnumerable<EolKind> Breaks(string text)
    {
        for (int i = text.AsSpan().IndexOfAny('\r', '\n'); i >= 0; i = NextBreak(text, i))
        {
            yield return KindAt(text, i);
        }
    }

    /// <summary>The next break after the one at <paramref name="i"/>; a CRLF pair counts once.</summary>
    private static int NextBreak(string text, int i)
    {
        int from = i + (KindAt(text, i) == EolKind.Windows ? 2 : 1);
        int next = from < text.Length ? text.AsSpan(from).IndexOfAny('\r', '\n') : -1;
        return next < 0 ? -1 : from + next;
    }

    private static EolKind KindAt(string text, int i) =>
        text[i] == '\n' ? EolKind.Unix
        : i + 1 < text.Length && text[i + 1] == '\n' ? EolKind.Windows
        : EolKind.Macintosh;
}
