using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Harness.Quality;

public sealed record MethodMetric(string Name, int Cyclomatic, int Lines, int MaxNesting);
public sealed record D3Report(
    int Files, int OwnedLoc, int Methods,
    double AvgCyclomatic, int MaxCyclomatic, int HighComplexityMethods,
    int MaxNesting, int WireCouplingCount, IReadOnlyList<string> WireCouplingSamples,
    IReadOnlyList<MethodMetric> Worst);

/// <summary>
/// D3 maintainability — pure static analysis (no build/boot) over the integration's OWN files (selected
/// by the profile's IntegrationPathPattern). Cyclomatic complexity + nesting via Roslyn syntax walking;
/// owned LOC; and the wire-coupling count — hand-maintained wire artifacts (endpoint-path string
/// literals, snake_case wire field-name literals, manual Base64/Basic auth construction). A generated
/// SDK hides these (≈0); a hand-rolled client owns them. This is the .NET (C#) adapter.
///
/// The wire-coupling regexes below are the REST/JSON default (path fragments ending .json + snake_case
/// fields). For a provider whose endpoints don't use .json suffixes, widen JsonUrl — see the playbook.
/// </summary>
public static class Metrics
{
    public static IReadOnlyList<string> IntegrationFiles(string treeRoot, string pathPattern)
    {
        var src = Path.Combine(treeRoot, "src");
        var root = Directory.Exists(src) ? src : treeRoot;
        var re = new Regex(pathPattern, RegexOptions.IgnoreCase);

        // Match the pattern against the path RELATIVE TO the tree root (leading '/'), never the absolute
        // path. Otherwise the tree's own root directory name and the repo path above it leak into every
        // match — a broad pattern matches every file whenever the tree's own root dir name happens to
        // contain one of the pattern's tokens. Relative matching keeps the pattern scoped to the
        // integration's own layout.
        static bool IsNoise(string p) =>
            p.Contains("/obj/") || p.Contains("/bin/") || p.EndsWith(".g.cs") ||
            p.Contains("AssemblyInfo") || Regex.IsMatch(p, @"/(Test|Tests)/", RegexOptions.IgnoreCase);

        var all = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsNoise(f.Replace('\\', '/'))).ToList();
        var selected = all
            .Where(f => re.IsMatch("/" + Path.GetRelativePath(treeRoot, f).Replace('\\', '/')))
            .OrderBy(f => f).ToList();

        // Plausibility guard: the integration's OWN provider-facing files are a small slice of a tree.
        // If the selection swallows most of the source, the pattern is almost certainly too broad —
        // surface it loudly instead of silently reporting inflated LOC/complexity.
        if (selected.Count > 25 && all.Count > 0 && selected.Count > all.Count * 0.5)
            Console.Error.WriteLine(
                $"[WARN] D3 selected {selected.Count} of {all.Count} source files — integrationPathPattern " +
                $"'{pathPattern}' looks too broad (matching the tree's own directory name?). Expected only " +
                "the integration's own provider-facing files.");

        return selected;
    }

    static readonly Regex JsonUrl = new(@"""[^""]*\.json[^""]*""", RegexOptions.Compiled);
    static readonly Regex SnakeCase = new(@"""[a-z][a-z0-9]*(_[a-z0-9]+)+""", RegexOptions.Compiled);
    static readonly Regex Base64Auth = new(@"Convert\.ToBase64String|""Basic ""|Base64", RegexOptions.Compiled);

    public static D3Report Analyze(string treeRoot, string pathPattern)
    {
        var files = IntegrationFiles(treeRoot, pathPattern);
        int ownedLoc = 0, wire = 0;
        var wireSamples = new List<string>();
        var methods = new List<MethodMetric>();

        foreach (var f in files)
        {
            var text = File.ReadAllText(f);
            ownedLoc += text.Split('\n').Count(l => { var t = l.Trim(); return t.Length > 0 && !t.StartsWith("//"); });

            foreach (Match m in JsonUrl.Matches(text)) { wire++; if (wireSamples.Count < 12) wireSamples.Add(m.Value); }
            foreach (Match m in SnakeCase.Matches(text)) { wire++; if (wireSamples.Count < 12) wireSamples.Add(m.Value); }
            wire += Base64Auth.Matches(text).Count;

            var rootNode = CSharpSyntaxTree.ParseText(text).GetRoot();
            foreach (var mth in rootNode.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                var body = (Microsoft.CodeAnalysis.SyntaxNode?)mth.Body ?? mth.ExpressionBody;
                if (body is null) continue;
                methods.Add(new MethodMetric(MethodName(mth), Cyclomatic(body), LineSpan(mth), MaxNesting(body)));
            }
        }

        var cc = methods.Select(m => m.Cyclomatic).DefaultIfEmpty(0).ToList();
        return new D3Report(
            Files: files.Count, OwnedLoc: ownedLoc, Methods: methods.Count,
            AvgCyclomatic: methods.Count == 0 ? 0 : Math.Round(cc.Average(), 2),
            MaxCyclomatic: cc.Max(),
            HighComplexityMethods: methods.Count(m => m.Cyclomatic > 10),
            MaxNesting: methods.Select(m => m.MaxNesting).DefaultIfEmpty(0).Max(),
            WireCouplingCount: wire, WireCouplingSamples: wireSamples,
            Worst: methods.OrderByDescending(m => m.Cyclomatic).Take(5).ToList());
    }

    static string MethodName(BaseMethodDeclarationSyntax m) => m switch
    {
        MethodDeclarationSyntax md => md.Identifier.Text,
        ConstructorDeclarationSyntax cd => cd.Identifier.Text + " (ctor)",
        _ => m.Kind().ToString(),
    };

    static int Cyclomatic(Microsoft.CodeAnalysis.SyntaxNode body)
    {
        int n = 1;
        foreach (var node in body.DescendantNodes())
        {
            switch (node)
            {
                case IfStatementSyntax:
                case WhileStatementSyntax:
                case ForStatementSyntax:
                case ForEachStatementSyntax:
                case CaseSwitchLabelSyntax:
                case CasePatternSwitchLabelSyntax:
                case SwitchExpressionArmSyntax:
                case ConditionalExpressionSyntax:
                case CatchClauseSyntax:
                    n++; break;
                case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.LogicalAndExpression) || b.IsKind(SyntaxKind.LogicalOrExpression):
                    n++; break;
                case BinaryExpressionSyntax b2 when b2.IsKind(SyntaxKind.CoalesceExpression):
                    n++; break;
            }
        }
        return n;
    }

    static int LineSpan(Microsoft.CodeAnalysis.SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    static int MaxNesting(Microsoft.CodeAnalysis.SyntaxNode body)
    {
        int max = 0;
        void Walk(Microsoft.CodeAnalysis.SyntaxNode n, int depth)
        {
            max = Math.Max(max, depth);
            foreach (var c in n.ChildNodes())
            {
                var deeper = c is BlockSyntax or IfStatementSyntax or ForStatementSyntax or
                    ForEachStatementSyntax or WhileStatementSyntax or SwitchStatementSyntax or TryStatementSyntax;
                Walk(c, depth + (deeper ? 1 : 0));
            }
        }
        Walk(body, 0);
        return max;
    }
}
