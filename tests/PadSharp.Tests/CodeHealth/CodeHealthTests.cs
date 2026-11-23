using System.Globalization;
using System.Text;
using Avalonia.Headless.XUnit;

namespace PadSharp.Tests.CodeHealth;

/// <summary>
/// The code-health gate over <c>src/</c>: every method, type and copied block must stay within the limits below.
/// A breach is allowed only as an entry in <see cref="Exceptions"/> with a written reason, and an exception that is
/// no longer needed fails too, so the list can only shrink. Limits are a ratchet: lower one when the code allows it,
/// never raise it to make a change pass. Set <c>PADSHARP_CODE_HEALTH_REPORT=&lt;file&gt;</c> to get the full report.
/// </summary>
public sealed class CodeHealthTests
{
    internal const int MaxLines = 30;
    internal const int MaxCyclomatic = 10;
    internal const int MaxCognitive = 15;
    internal const int MaxNesting = 2;
    internal const int MaxParameters = 4;
    internal const int MaxTypeLines = 500;
    internal const double MaxDuplicationPercent = 3.0;

    /// <summary>
    /// Allowed breaches: <c>Type.Method</c> (or <c>file.cs</c> for a type) → why it is better as it is. Keep this short;
    /// every entry needs a reason a reviewer would accept.
    /// </summary>
    internal static readonly Dictionary<string, string> Exceptions = new(StringComparer.Ordinal);

    private static readonly Lazy<CodeReport> Report = new(() =>
    {
        CodeReport report = CodeMetrics.Analyze(Path.Combine(RepoRoot(), "src"));
        if (Environment.GetEnvironmentVariable("PADSHARP_CODE_HEALTH_REPORT") is { Length: > 0 } path)
        {
            File.WriteAllText(path, Describe(report));
        }

        return report;
    });

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PadSharp.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("PadSharp.slnx not found above the test output.");
    }

    /// <summary>The limits a method breaks, e.g. "lines 42 > 30".</summary>
    internal static IReadOnlyList<string> Breaches(MethodMetrics m)
    {
        var broken = new List<string>();
        void Check(string name, int value, int limit)
        {
            if (value > limit)
            {
                broken.Add($"{name} {value} > {limit}");
            }
        }

        Check("lines", m.Lines, MaxLines);
        Check("cyclomatic", m.Cyclomatic, MaxCyclomatic);
        Check("cognitive", m.Cognitive, MaxCognitive);
        Check("nesting", m.Nesting, MaxNesting);
        Check("parameters", m.Parameters, MaxParameters);
        return broken;
    }

    [AvaloniaFact]
    public void MethodsStayWithinTheLimits()
    {
        string[] over = [.. Report.Value.Methods
            .Where(m => Breaches(m).Count > 0 && !Exceptions.ContainsKey(m.Key))
            .Select(m => $"{m.Key} ({m.Location}): {string.Join(", ", Breaches(m))}")];

        Assert.True(over.Length == 0, "Over the code-health limits (split the method, or add a motivated exception):\n" + string.Join("\n", over));
    }

    [AvaloniaFact]
    public void TypesStayWithinTheLimit()
    {
        string[] over = [.. Report.Value.Types
            .Where(t => t.Lines > MaxTypeLines && !Exceptions.ContainsKey(t.File))
            .Select(t => $"{t.Name} in {t.File}: {t.Lines} lines > {MaxTypeLines}")];

        Assert.True(over.Length == 0, "Types over the limit (split by seam, e.g. into another partial file):\n" + string.Join("\n", over));
    }

    [AvaloniaFact]
    public void CodeIsNotCopied()
    {
        CodeReport report = Report.Value;

        Assert.True(report.DuplicationPercent <= MaxDuplicationPercent,
            $"{report.DuplicationPercent:0.0} % of code lines are copied:\n" + string.Join("\n", report.Duplicates));
    }

    [AvaloniaFact]
    public void EveryExceptionIsMotivatedAndStillNeeded() =>
        Assert.Empty(ExceptionProblems(Exceptions, Report.Value));

    [AvaloniaFact]
    public void AnUnmotivatedOrStaleExceptionIsRejected()
    {
        var report = new CodeReport(
            [new MethodMetrics("a.cs", 1, "A", "Long", Lines: 99, Cyclomatic: 1, Cognitive: 0, Nesting: 0, Parameters: 0),
             new MethodMetrics("a.cs", 9, "A", "Short", Lines: 5, Cyclomatic: 1, Cognitive: 0, Nesting: 0, Parameters: 0)],
            [], CodeLines: 100, DuplicatedLines: 0, Duplicates: []);
        var exceptions = new Dictionary<string, string>
        {
            ["A.Long"] = "too long",
            ["A.Short"] = "A generated table that reads best as one list.",
        };

        Assert.Equal(
            ["The exception for A.Long needs a real reason.", "A.Short is within the limits now: remove its exception."],
            ExceptionProblems(exceptions, report));
    }

    /// <summary>Exceptions without a real reason (20+ characters), and exceptions the code no longer needs.</summary>
    internal static List<string> ExceptionProblems(IReadOnlyDictionary<string, string> exceptions, CodeReport report)
    {
        var problems = new List<string>();
        foreach ((string key, string reason) in exceptions)
        {
            if (reason.Trim().Length < 20)
            {
                problems.Add($"The exception for {key} needs a real reason.");
            }

            bool needed = report.Methods.Any(m => m.Key == key && Breaches(m).Count > 0)
                || report.Types.Any(t => t.File == key && t.Lines > MaxTypeLines);
            if (!needed)
            {
                problems.Add($"{key} is within the limits now: remove its exception.");
            }
        }

        return problems;
    }

    [AvaloniaFact]
    public void TheMeasuringCountsWhatItShould()
    {
        const string source = """
            class C
            {
                int M(int a, int b)
                {
                    if (a > 0 && b > 0)
                    {
                        foreach (var x in new[] { 1 })
                        {
                            if (x == 1) { return 1; }
                        }
                    }
                    else if (a < 0)
                    {
                        return a ?? 0;
                    }

                    return b;
                }
            }
            """;

        MethodMetrics m = Assert.Single(CodeMetrics.Measure(source));

        Assert.Equal(("C.M", 2, 16), (m.Key, m.Parameters, m.Lines));
        Assert.Equal(7, m.Cyclomatic);
        Assert.Equal(3, m.Nesting);
        Assert.Equal(8, m.Cognitive);
    }

    /// <summary>The report the code-health skill reads: scorecard, worst methods, largest types, everything over a limit.</summary>
    private static string Describe(CodeReport report)
    {
        var sb = new StringBuilder();
        IReadOnlyList<MethodMetrics> ms = report.Methods;
        sb.AppendLine(CultureInfo.InvariantCulture, $"methods {ms.Count}, types {report.Types.Count}, code lines {report.CodeLines}, copied {report.DuplicationPercent:0.0} %");
        (string Name, Func<MethodMetrics, int> Value, int Good, int Limit)[] metrics =
        [
            ("lines", m => m.Lines, 15, MaxLines), ("cyclomatic", m => m.Cyclomatic, 5, MaxCyclomatic),
            ("cognitive", m => m.Cognitive, 5, MaxCognitive), ("nesting", m => m.Nesting, 1, MaxNesting),
            ("parameters", m => m.Parameters, 3, MaxParameters),
        ];
        sb.AppendLine("| Metric | Good | Acceptable | Over | Worst |").AppendLine("|---|---|---|---|---|");
        foreach ((string name, Func<MethodMetrics, int> value, int good, int limit) in metrics)
        {
            MethodMetrics worst = ms.MaxBy(value)!;
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"| {name} (≤{good} / ≤{limit}) | {ms.Count(m => value(m) <= good)} | {ms.Count(m => value(m) > good && value(m) <= limit)} | {ms.Count(m => value(m) > limit)} | {value(worst)} `{worst.Key}` |");
        }

        sb.AppendLine().AppendLine("Largest types:");
        foreach (TypeMetrics t in report.Types.OrderByDescending(t => t.Lines).Take(8))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {t.Lines,5}  {t.Name}  {t.File}");
        }

        sb.AppendLine().AppendLine("Top by cognitive complexity:");
        foreach (MethodMetrics m in ms.OrderByDescending(m => m.Cognitive).ThenByDescending(m => m.Cyclomatic).Take(10))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {m.Lines,4} L  cc {m.Cyclomatic,2}  cog {m.Cognitive,2}  nest {m.Nesting}  p {m.Parameters}  {m.Key}  {m.Location}");
        }

        sb.AppendLine().AppendLine("Over a limit:");
        foreach (MethodMetrics m in ms.Where(m => Breaches(m).Count > 0))
        {
            string mark = Exceptions.TryGetValue(m.Key, out string? why) ? $"  [exception: {why}]" : string.Empty;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {m.Key} ({m.Location}): {string.Join(", ", Breaches(m))}{mark}");
        }

        return sb.ToString();
    }
}
