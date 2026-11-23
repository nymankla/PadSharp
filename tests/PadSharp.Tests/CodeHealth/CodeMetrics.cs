using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace PadSharp.Tests.CodeHealth;

/// <summary>One method, accessor (5+ lines) or local function.</summary>
internal sealed record MethodMetrics(string File, int Line, string Type, string Name, int Lines, int Cyclomatic, int Cognitive, int Nesting, int Parameters)
{
    /// <summary>The key an exception is written under, e.g. <c>MainWindow.BuildMenu</c>.</summary>
    public string Key => $"{Type}.{Name}";

    public string Location => $"{File}:{Line}";
}

/// <summary>One type declaration; a partial class counts once per file.</summary>
internal sealed record TypeMetrics(string File, string Name, int Lines);

internal sealed record CodeReport(IReadOnlyList<MethodMetrics> Methods, IReadOnlyList<TypeMetrics> Types, int CodeLines, int DuplicatedLines, IReadOnlyList<string> Duplicates)
{
    public double DuplicationPercent => CodeLines == 0 ? 0 : 100.0 * DuplicatedLines / CodeLines;
}

/// <summary>
/// Syntax-only Roslyn metrics (no compilation, so fast and reference-free) for the code-health gate and the
/// code-health skill's report: length, cyclomatic and cognitive complexity, nesting, parameters, type size and
/// copied blocks.
/// </summary>
internal static class CodeMetrics
{
    private const int DuplicateWindow = 10;

    public static CodeReport Analyze(string folder)
    {
        var methods = new List<MethodMetrics>();
        var types = new List<TypeMetrics>();
        var windows = new Dictionary<string, List<(string File, int Line)>>();
        int codeLines = 0;
        foreach (string path in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            string file = Path.GetRelativePath(folder, path).Replace('\\', '/');
            string text = File.ReadAllText(path);
            string[] lines = text.Split('\n');
            codeLines += lines.Count(l => l.Trim() is { Length: > 0 } t && !t.StartsWith("//", StringComparison.Ordinal));
            AddWindows(windows, file, lines);
            SyntaxNode root = CSharpSyntaxTree.ParseText(text).GetRoot();
            types.AddRange(root.DescendantNodes().OfType<TypeDeclarationSyntax>().Select(t => new TypeMetrics(file, t.Identifier.Text, LineCount(t))));
            methods.AddRange(MeasureMethods(root, file));
        }

        List<List<(string File, int Line)>> copies = [.. windows.Values.Where(v => v.Count > 1)];
        int duplicated = copies.SelectMany(v => v.SelectMany(w => Enumerable.Range(w.Line, DuplicateWindow).Select(l => (w.File, l)))).Distinct().Count();
        return new CodeReport(methods, types, codeLines, duplicated, [.. copies.Select(v => string.Join(" = ", v.Select(w => $"{w.File}:{w.Line}")))]);
    }

    /// <summary>Measures the methods in one piece of source (used to test the measuring itself).</summary>
    public static IReadOnlyList<MethodMetrics> Measure(string source) =>
        [.. MeasureMethods(CSharpSyntaxTree.ParseText(source).GetRoot(), "test.cs")];

    private static IEnumerable<MethodMetrics> MeasureMethods(SyntaxNode root, string file)
    {
        foreach (SyntaxNode m in root.DescendantNodes().Where(n => n is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax))
        {
            if (Body(m) is not { } body || (m is AccessorDeclarationSyntax && LineCount(m) < 5))
            {
                continue;
            }

            // Local functions are measured on their own, so they don't count towards the method around them.
            List<SyntaxNode> nodes = [.. body.DescendantNodesAndSelf(n => n == body || n is not LocalFunctionStatementSyntax)];
            (int cognitive, int nesting) = Cognitive(body);
            yield return new MethodMetrics(file, m.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                m.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? string.Empty, Name(m),
                LineCount(m), 1 + nodes.Count(IsDecisionPoint), cognitive, nesting, ParameterCount(m));
        }
    }

    private static SyntaxNode? Body(SyntaxNode m) => m switch
    {
        BaseMethodDeclarationSyntax b => (SyntaxNode?)b.Body ?? b.ExpressionBody,
        AccessorDeclarationSyntax a => (SyntaxNode?)a.Body ?? a.ExpressionBody,
        LocalFunctionStatementSyntax l => (SyntaxNode?)l.Body ?? l.ExpressionBody,
        _ => null,
    };

    private static string Name(SyntaxNode m) => m switch
    {
        MethodDeclarationSyntax method => method.Identifier.Text,
        ConstructorDeclarationSyntax => ".ctor",
        LocalFunctionStatementSyntax local => "local:" + local.Identifier.Text,
        AccessorDeclarationSyntax accessor => (accessor.Parent?.Parent switch
        {
            PropertyDeclarationSyntax p => p.Identifier.Text,
            EventDeclarationSyntax e => e.Identifier.Text,
            _ => "this[]",
        }) + "." + accessor.Keyword.Text,
        OperatorDeclarationSyntax op => "op" + op.OperatorToken.Text,
        _ => m.Kind().ToString(),
    };

    private static int ParameterCount(SyntaxNode m) => m switch
    {
        BaseMethodDeclarationSyntax b => b.ParameterList.Parameters.Count,
        LocalFunctionStatementSyntax l => l.ParameterList.Parameters.Count,
        _ => 0,
    };

    private static int LineCount(SyntaxNode node)
    {
        FileLinePositionSpan span = node.GetLocation().GetLineSpan();
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    /// <summary>Cyclomatic complexity: one plus each branch, loop, case, catch and short-circuit operator.</summary>
    private static bool IsDecisionPoint(SyntaxNode n) => n.Kind() is SyntaxKind.IfStatement or SyntaxKind.WhileStatement
        or SyntaxKind.ForStatement or SyntaxKind.ForEachStatement or SyntaxKind.ForEachVariableStatement or SyntaxKind.DoStatement
        or SyntaxKind.CaseSwitchLabel or SyntaxKind.CasePatternSwitchLabel or SyntaxKind.SwitchExpressionArm or SyntaxKind.CatchClause
        or SyntaxKind.ConditionalExpression or SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression
        or SyntaxKind.CoalesceExpression or SyntaxKind.CoalesceAssignmentExpression or SyntaxKind.ConditionalAccessExpression;

    /// <summary>
    /// Sonar-style cognitive complexity (approximation): each branch or loop adds one plus its nesting, <c>else</c> and
    /// each run of the same <c>&amp;&amp;</c>/<c>||</c> add one, lambdas nest without adding. Also the deepest nesting.
    /// </summary>
    private static (int Score, int MaxNesting) Cognitive(SyntaxNode body)
    {
        int score = 0, maxNesting = 0;
        Walk(body, 0);
        return (score, maxNesting);

        void Walk(SyntaxNode node, int nesting)
        {
            foreach (SyntaxNode child in node.ChildNodes())
            {
                if (child is LocalFunctionStatementSyntax)
                {
                    continue;
                }

                if (child is IfStatementSyntax { Parent: ElseClauseSyntax })
                {
                    score++;
                    Walk(child, nesting);
                    continue;
                }

                (int increment, bool nests, bool isLambda) = Weigh(child, nesting);
                score += increment;
                int next = nests ? nesting + 1 : nesting;
                if (nests && !isLambda)
                {
                    maxNesting = Math.Max(maxNesting, next);
                }

                Walk(child, next);
            }
        }
    }

    private static (int Increment, bool Nests, bool IsLambda) Weigh(SyntaxNode child, int nesting) => child switch
    {
        IfStatementSyntax => (1 + nesting, true, false),
        ElseClauseSyntax { Statement: not IfStatementSyntax } => (1, false, false),
        WhileStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax
            or DoStatementSyntax or SwitchStatementSyntax or SwitchExpressionSyntax or CatchClauseSyntax
            or ConditionalExpressionSyntax => (1 + nesting, true, false),
        LambdaExpressionSyntax or AnonymousMethodExpressionSyntax => (0, true, true),
        BinaryExpressionSyntax b when b.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression
            => (b.Parent is BinaryExpressionSyntax p && p.Kind() == b.Kind() ? 0 : 1, false, false),
        GotoStatementSyntax => (1, false, false),
        _ => (0, false, false),
    };

    private static void AddWindows(Dictionary<string, List<(string File, int Line)>> windows, string file, string[] lines)
    {
        List<(string Text, int Line)> code = [.. lines.Select((l, i) => (Text: l.Trim(), Line: i + 1))
            .Where(x => x.Text.Length > 2 && !x.Text.StartsWith("//", StringComparison.Ordinal) && !x.Text.StartsWith("using ", StringComparison.Ordinal)
                        && x.Text is not ("{" or "}" or "};" or "});" or ")" or ");"))];
        for (int i = 0; i + DuplicateWindow <= code.Count; i++)
        {
            string block = string.Join("\n", code.Skip(i).Take(DuplicateWindow).Select(x => x.Text));
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(block)));
            if (!windows.TryGetValue(key, out List<(string File, int Line)>? list))
            {
                windows[key] = list = [];
            }

            list.Add((file, code[i].Line));
        }
    }
}
