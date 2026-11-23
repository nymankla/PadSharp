using Avalonia;

namespace PadSharp;

internal static class Program
{
    private static readonly char[] WildcardChars = ['*', '?'];

    private static readonly EnumerationOptions WildcardMatching = new()
    {
        MatchType = MatchType.Simple,
        MatchCasing = MatchCasing.PlatformDefault,
        IgnoreInaccessible = true,
    };

    /// <summary>
    /// Starts the editor. Arguments are files to open (<c>*</c> and <c>?</c> in a file name are expanded);
    /// <c>--screenshot file.png</c> draws about 120 frames without taking focus, saves a picture of the window and
    /// exits. When PadSharp is already running, the files go to that window instead and this process exits at once.
    /// </summary>
    [STAThread]
    public static int Main(string[] args)
    {
        string[] expanded = ExpandWildcards(args);
        return ForwardToRunningInstance(expanded) ? 0 : BuildAvaloniaApp().StartWithClassicDesktopLifetime(expanded);
    }

    /// <summary>Used by the app and by design tools.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>
    /// True when another PadSharp owns the session and took the files. Otherwise this process claims the session
    /// (so the window it opens keeps tabs between runs) and starts normally. Screenshot runs never forward.
    /// </summary>
    internal static bool ForwardToRunningInstance(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.Contains("--screenshot") || Session.TryClaim())
        {
            return false;
        }

        return SingleInstance.TrySend(SingleInstance.PipeName, args, TimeSpan.FromSeconds(3));
    }

    /// <summary>
    /// Replaces <c>*.cs</c>, <c>src/note?.md</c> and the like with the matching files, sorted, because Windows
    /// shells pass patterns on unexpanded. Only the file name may hold wildcards. A pattern that matches nothing,
    /// or names a file that really exists, stays as it is; the value after <c>--screenshot</c> is left alone.
    /// </summary>
    internal static string[] ExpandWildcards(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var result = new List<string>();
        for (int i = 0; i < args.Count; i++)
        {
            bool screenshotValue = i > 0 && args[i - 1] == "--screenshot";
            result.AddRange(screenshotValue ? [args[i]] : Expand(args[i]));
        }

        return [.. result];
    }

    private static string[] Expand(string arg)
    {
        string name = Path.GetFileName(arg);
        if (name.IndexOfAny(WildcardChars) < 0 || File.Exists(arg))
        {
            return [arg];
        }

        string folder = Path.GetDirectoryName(arg) is { Length: > 0 } dir ? dir : ".";
        string[] matches = Matches(folder, name);
        return matches.Length > 0 ? matches : [arg];
    }

    private static string[] Matches(string folder, string pattern)
    {
        try
        {
            return Directory.Exists(folder)
                ? [.. Directory.EnumerateFiles(folder, pattern, WildcardMatching).Order(StringComparer.OrdinalIgnoreCase)]
                : [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }
}
