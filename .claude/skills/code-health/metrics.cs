#:package Microsoft.CodeAnalysis.CSharp@5.9.0
#:property PublishAot=false

// Code-health metrics for PadSharp (syntax only, no compilation). Usage, from the repo root:
//   dotnet run .claude/skills/code-health/metrics.cs [root] [--json report.json]
// Prints the scorecard per band, the hotspots, the largest types and files, and duplicated 10-line blocks.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

string root = Path.GetFullPath(args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal) && !a.EndsWith(".json", StringComparison.Ordinal)) ?? ".");
int jsonAt = Array.IndexOf(args, "--json");
string? jsonPath = jsonAt >= 0 && jsonAt + 1 < args.Length ? args[jsonAt + 1] : null;

// Bands: good / acceptable; above acceptable is the warning band (refactoring candidates).
(string Name, Func<Method, int> Value, int Good, int Acceptable)[] bands =
[
    ("Lines", m => m.Lines, 15, 30),
    ("Cyclomatic", m => m.Cc, 5, 10),
    ("Cognitive", m => m.Cognitive, 5, 15),
    ("Nesting", m => m.Nesting, 1, 2),
    ("Parameters", m => m.Parameters, 3, 4),
];
const int TypeGood = 199, TypeAcceptable = 500, Window = 10;
string[] skipped = ["bin", "obj", "publish", "artifacts", ".claude", ".git", ".vs"];

var methods = new List<Method>();
var types = new List<TypeInfo>();
var files = new List<(string File, int Code)>();
var windows = new Dictionary<string, List<(string File, int Line)>>();

foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
    .Where(f => Path.GetRelativePath(root, f).Split(Path.DirectorySeparatorChar).SkipLast(1).All(d => !skipped.Contains(d)))
    .Order(StringComparer.Ordinal))
{
    string rel = Path.GetRelativePath(root, path).Replace('\\', '/');
    string text = File.ReadAllText(path);
    string[] lines = text.Split('\n');
    files.Add((rel, lines.Count(l => l.Trim() is { Length: > 0 } t && !t.StartsWith("//", StringComparison.Ordinal))));
    AddWindows(rel, lines);

    SyntaxNode tree = CSharpSyntaxTree.ParseText(text).GetRoot();
    foreach (var t in tree.DescendantNodes().OfType<TypeDeclarationSyntax>())
    {
        types.Add(new TypeInfo(rel, t.Identifier.Text, t.Keyword.Text, LineCount(t), t.Members.Count,
            t.Members.OfType<FieldDeclarationSyntax>().Sum(f => f.Declaration.Variables.Count)));
    }

    foreach (var m in tree.DescendantNodes().Where(n => n is BaseMethodDeclarationSyntax or AccessorDeclarationSyntax or LocalFunctionStatementSyntax))
    {
        if (Body(m) is not { } body || (m is AccessorDeclarationSyntax && LineCount(m) < 5))
        {
            continue;
        }

        var nodes = body.DescendantNodesAndSelf(n => n == body || n is not LocalFunctionStatementSyntax).ToList();
        (int cognitive, int nesting) = Cognitive(body);
        methods.Add(new Method(rel, m.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            m.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.Text ?? "", Name(m),
            LineCount(m), 1 + nodes.Count(IsDecisionPoint), cognitive, nesting, ParameterCount(m)));
    }
}

var duplicates = windows.Values.Where(v => v.Count > 1).ToList();
int codeLines = files.Sum(f => f.Code);
int duplicated = duplicates.SelectMany(v => v.SelectMany(w => Enumerable.Range(w.Line, Window).Select(l => (w.File, l)))).Distinct().Count();

Console.WriteLine($"files {files.Count}, code lines {codeLines}, methods {methods.Count}, types {types.Count}");
Console.WriteLine();
Console.WriteLine("| Metric | Good | Acceptable | Warning | Worst |");
Console.WriteLine("|---|---|---|---|---|");
foreach (var (name, value, good, acceptable) in bands)
{
    Method worst = methods.MaxBy(value)!;
    Console.WriteLine($"| {name} (≤{good} / ≤{acceptable}) | {methods.Count(m => value(m) <= good)} | " +
        $"{methods.Count(m => value(m) > good && value(m) <= acceptable)} | {methods.Count(m => value(m) > acceptable)} | " +
        $"{value(worst)} `{worst.Type}.{worst.Name}` |");
}

TypeInfo biggest = types.MaxBy(t => t.Lines)!;
Console.WriteLine($"| Type lines (≤{TypeGood} / ≤{TypeAcceptable}) | {types.Count(t => t.Lines <= TypeGood)} | " +
    $"{types.Count(t => t.Lines > TypeGood && t.Lines <= TypeAcceptable)} | {types.Count(t => t.Lines > TypeAcceptable)} | {biggest.Lines} `{biggest.Name}` |");
Console.WriteLine($"| Duplication | {(codeLines == 0 ? 0 : 100.0 * duplicated / codeLines):0.0} % of code lines in {duplicates.Count} repeated {Window}-line blocks | | | |");

string Row(Method m) => $"{m.Lines,4} L  cc {m.Cc,3}  cog {m.Cognitive,3}  nest {m.Nesting}  p {m.Parameters}  {m.Type}.{m.Name}  {m.File}:{m.Line}";
Section("Top 15 by cognitive complexity", methods.OrderByDescending(m => m.Cognitive).ThenByDescending(m => m.Cc).Take(15).Select(Row));
Section("Top 15 by length", methods.OrderByDescending(m => m.Lines).Take(15).Select(Row));
Section("Largest types", types.OrderByDescending(t => t.Lines).Take(10).Select(t => $"{t.Lines,5} L  members {t.Members,3}  fields {t.Fields,2}  {t.Kind} {t.Name}  {t.File}"));
Section("Largest files", files.OrderByDescending(f => f.Code).Take(10).Select(f => $"{f.Code,5}  {f.File}"));
Section("Warning band, per file", methods
    .Select(m => (m, Broken: bands.Where(b => b.Value(m) > b.Acceptable).Select(b => $"{b.Name.ToLowerInvariant()} {b.Value(m)}").ToList()))
    .Where(x => x.Broken.Count > 0)
    .GroupBy(x => x.m.File)
    .OrderByDescending(g => g.Count())
    .SelectMany(g => g.Select(x => $"   {x.m.Type}.{x.m.Name}:{x.m.Line}  {string.Join(", ", x.Broken)}").Prepend(g.Key)));
Section("Duplicated blocks", duplicates.Select(v => string.Join("  ,  ", v.Select(w => $"{w.File}:{w.Line}"))));

if (jsonPath is not null)
{
    File.WriteAllText(jsonPath, JsonSerializer.Serialize(new { methods, types, files = files.Select(f => new { f.File, f.Code }), duplicatedLines = duplicated, codeLines },
        new JsonSerializerOptions { WriteIndented = true, IncludeFields = true }));
    Console.WriteLine($"\nreport: {jsonPath}");
}

static void Section(string title, IEnumerable<string> rows)
{
    Console.WriteLine($"\n== {title}");
    foreach (string row in rows)
    {
        Console.WriteLine(row);
    }
}

void AddWindows(string file, string[] lines)
{
    var code = lines.Select((l, i) => (Text: l.Trim(), Line: i + 1))
        .Where(x => x.Text.Length > 2 && !x.Text.StartsWith("//", StringComparison.Ordinal) && !x.Text.StartsWith("using ", StringComparison.Ordinal)
                    && x.Text is not ("{" or "}" or "};" or "});" or ")" or ");"))
        .ToList();
    for (int i = 0; i + Window <= code.Count; i++)
    {
        string block = string.Join("\n", code.Skip(i).Take(Window).Select(x => x.Text));
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(block)));
        if (!windows.TryGetValue(key, out var list))
        {
            windows[key] = list = [];
        }

        list.Add((file, code[i].Line));
    }
}

static SyntaxNode? Body(SyntaxNode m) => m switch
{
    BaseMethodDeclarationSyntax b => (SyntaxNode?)b.Body ?? b.ExpressionBody,
    AccessorDeclarationSyntax a => (SyntaxNode?)a.Body ?? a.ExpressionBody,
    LocalFunctionStatementSyntax l => (SyntaxNode?)l.Body ?? l.ExpressionBody,
    _ => null,
};

static string Name(SyntaxNode m) => m switch
{
    MethodDeclarationSyntax md => md.Identifier.Text,
    ConstructorDeclarationSyntax => ".ctor",
    LocalFunctionStatementSyntax lf => "local:" + lf.Identifier.Text,
    AccessorDeclarationSyntax ad => (ad.Parent?.Parent switch
    {
        PropertyDeclarationSyntax p => p.Identifier.Text,
        EventDeclarationSyntax e => e.Identifier.Text,
        _ => "this[]",
    }) + "." + ad.Keyword.Text,
    OperatorDeclarationSyntax od => "op" + od.OperatorToken.Text,
    _ => m.Kind().ToString(),
};

static int ParameterCount(SyntaxNode m) => m switch
{
    BaseMethodDeclarationSyntax b => b.ParameterList.Parameters.Count,
    LocalFunctionStatementSyntax l => l.ParameterList.Parameters.Count,
    _ => 0,
};

static int LineCount(SyntaxNode node)
{
    var span = node.GetLocation().GetLineSpan();
    return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
}

// Cyclomatic complexity: one plus each branch, loop, case, catch and short-circuit operator.
static bool IsDecisionPoint(SyntaxNode n) => n.Kind() is SyntaxKind.IfStatement or SyntaxKind.WhileStatement
    or SyntaxKind.ForStatement or SyntaxKind.ForEachStatement or SyntaxKind.ForEachVariableStatement or SyntaxKind.DoStatement
    or SyntaxKind.CaseSwitchLabel or SyntaxKind.CasePatternSwitchLabel or SyntaxKind.SwitchExpressionArm or SyntaxKind.CatchClause
    or SyntaxKind.ConditionalExpression or SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression
    or SyntaxKind.CoalesceExpression or SyntaxKind.CoalesceAssignmentExpression or SyntaxKind.ConditionalAccessExpression;

// Sonar-style cognitive complexity (approximation): each branch or loop adds one plus its nesting, else and each run of
// the same &&/|| add one, lambdas nest without adding. Also returns the deepest control-flow nesting.
static (int Score, int MaxNesting) Cognitive(SyntaxNode body)
{
    int score = 0, maxNesting = 0;
    Walk(body, 0);
    return (score, maxNesting);

    void Walk(SyntaxNode node, int nesting)
    {
        foreach (var child in node.ChildNodes())
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

            var (increment, nests, isLambda) = child switch
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

internal sealed record Method(string File, int Line, string Type, string Name, int Lines, int Cc, int Cognitive, int Nesting, int Parameters);

internal sealed record TypeInfo(string File, string Name, string Kind, int Lines, int Members, int Fields);
