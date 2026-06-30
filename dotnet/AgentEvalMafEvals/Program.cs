// SPDX-License-Identifier: MIT
// ──────────────────────────────────────────────────────────────────────────────
//  AgentEval × Microsoft Agent Framework — agent evals & benchmarks
//
//  A menu-driven console app that scores a real MAF agent with AgentEval and renders
//  self-contained HTML reports. Three categories:
//    • Flat metrics       — a custom metric bundle via MAF's native IAgentEvaluator
//    • Agentic benchmark  — pick a preset composite (also IAgentEvaluator)
//    • Red-team benchmark — OWASP / MITRE / NIST × smoke/standard/audit (AgentEval ScanAsync)
//
//  Modes:
//    dotnet run                              interactive category MENU (loops until you quit)
//    dotnet run -- --benchmark reasoning     one-shot: flat metrics + that Agentic benchmark, then exit
//    dotnet run -- --no-open                 one-shot default benchmark, don't launch the browser (CI)
//    dotnet run -- --menu                    force the menu even when stdin is piped (scripting/tests)
//
//  Needs: AZURE_OPENAI_ENDPOINT + _API_KEY + _DEPLOYMENT, or OPENAI_API_KEY.
//  Everything ships in the `AgentEval` NuGet package (>= 0.13.2-beta; depends on Microsoft.Agents.AI 1.11.1).
// ──────────────────────────────────────────────────────────────────────────────

using AgentEvalMafEvals;
using AgentEvalMafEvals.Evaluation;
using AgentEvalMafEvals.Infrastructure;

Output.Init();
Output.Banner(
    "AgentEval × MAF — agent evals & benchmarks",
    "Metrics · Agentic benchmarks · Red-team (OWASP/MITRE/NIST) — scored and rendered to HTML");

if (!AiBackend.IsConfigured)
{
    Output.SkipBox();
    return;
}

var open = !args.Contains("--no-open", StringComparer.OrdinalIgnoreCase);
var forceMenu = args.Contains("--menu", StringComparer.OrdinalIgnoreCase);

var backend = AiBackend.Resolve();
Output.Kv("Backend", backend.Backend);

var app = new EvalApp(backend, open);

// One-shot (automation): --benchmark <agentic-key>, or a piped/CI run without --menu.
// Otherwise: the interactive category menu.
var fullRequested = args.Where((a, i) =>
    string.Equals(a, "--benchmark", StringComparison.OrdinalIgnoreCase) &&
    i + 1 < args.Length && string.Equals(args[i + 1], "full", StringComparison.OrdinalIgnoreCase)).Any();

var cliChoice = BenchmarkSelector.FromArgs(args);
if (!forceMenu && (fullRequested || cliChoice is not null || Console.IsInputRedirected))
{
    if (fullRequested)
        await app.RunFullSuiteAsync();
    else
        await app.RunOneShotAsync(cliChoice ?? BenchmarkCatalog.Default);
    return;
}

await app.RunInteractiveAsync();
