using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Quality;

public sealed record MethodMetric(string Name, int Cyclomatic, int Lines, int MaxNesting);
public sealed record D3Report(
    int Files, int OwnedLoc, int Methods,
    double AvgCyclomatic, int MaxCyclomatic, int HighComplexityMethods,
    int MaxNesting, int WireCouplingCount, IReadOnlyList<string> WireCouplingSamples,
    IReadOnlyList<MethodMetric> Worst);

/// <summary>
/// D3 maintainability — pure static analysis (no build/boot) over an arm's INTEGRATION files only
/// (paths containing Billing/ or Maxio/). Cyclomatic complexity + nesting via Roslyn syntax walking;
/// owned LOC; and the wire-coupling count — the sharpest, most objective SDK-vs-hand-rolled signal:
/// literal Maxio wire artifacts the integrator hand-maintains (".json" URL fragments, snake_case field
/// literals, base64 Basic-auth construction). A generated SDK hides these (≈0); hand-rolling owns them.
/// </summary>
public static class Metrics
{
    // integration files = added billing/maxio code, excluding the vanilla eShop baseline + tests
    public static IReadOnlyList<string> IntegrationFiles(string treeRoot)
    {
        var src = Path.Combine(treeRoot, "src");
        var root = Directory.Exists(src) ? src : treeRoot;
        return Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f =>
            {
                var p = f.Replace('\\', '/');
                var isIntegration = Regex.IsMatch(p, @"/(Billing|Maxio)", RegexOptions.IgnoreCase);
                var isNoise = p.Contains("/obj/") || p.Contains("/bin/") ||
                              p.EndsWith(".g.cs") || p.Contains("AssemblyInfo") ||
                              Regex.IsMatch(p, @"/(Test|Tests)/", RegexOptions.IgnoreCase);
                return isIntegration && !isNoise;
            })
            .OrderBy(f => f).ToList();
    }

    // wire artifacts a hand-rolled integration owns and an SDK hides
    static readonly Regex JsonUrl = new(@"""[^""]*\.json[^""]*""", RegexOptions.Compiled);
    static readonly Regex SnakeCase = new(@"""[a-z][a-z0-9]*(_[a-z0-9]+)+""", RegexOptions.Compiled);   // "price_in_cents"
    static readonly Regex Base64Auth = new(@"Convert\.ToBase64String|""Basic ""|Base64", RegexOptions.Compiled);

    public static D3Report Analyze(string treeRoot)
    {
        var files = IntegrationFiles(treeRoot);
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

            var treeRootNode = CSharpSyntaxTree.ParseText(text).GetRoot();
            foreach (var mth in treeRootNode.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
            {
                var body = (SyntaxNode?)mth.Body ?? mth.ExpressionBody;
                if (body is null) continue;
                var name = MethodName(mth);
                methods.Add(new MethodMetric(name, Cyclomatic(body), LineSpan(mth), MaxNesting(body)));
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

    // Cyclomatic complexity = 1 + count of branch points (McCabe, standard approximation)
    static int Cyclomatic(SyntaxNode body)
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

    static int LineSpan(SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
    }

    static int MaxNesting(SyntaxNode body)
    {
        int max = 0;
        void Walk(SyntaxNode n, int depth)
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
