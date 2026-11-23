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

    private static (string Text, TextEncodingKind Kind) Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), TextEncodingKind.Utf8Bom);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), TextEncodingKind.Utf16LeBom);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), TextEncodingKind.Utf16BeBom);
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
        bool crlf = false, lf = false, cr = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                bool pair = i + 1 < text.Length && text[i + 1] == '\n';
                crlf |= pair;
                cr |= !pair;
                i += pair ? 1 : 0;
            }
            else if (text[i] == '\n')
            {
                lf = true;
            }
        }

        return (crlf ? 1 : 0) + (lf ? 1 : 0) + (cr ? 1 : 0) > 1;
    }

    private static EolKind DetectEol(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                return i + 1 < text.Length && text[i + 1] == '\n' ? EolKind.Windows : EolKind.Macintosh;
            }

            if (text[i] == '\n')
            {
                return EolKind.Unix;
            }
        }

        return EolKind.Windows;
    }
}
