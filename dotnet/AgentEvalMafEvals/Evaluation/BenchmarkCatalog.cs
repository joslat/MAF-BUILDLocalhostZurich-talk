// SPDX-License-Identifier: MIT
// AgentEval × MAF — the selectable Agentic benchmark tiers + presets, and the full-suite category set.

using AgentEval.Benchmarks;                    // AgenticBenchmark
using AgentEval.Core;                          // ChatClientEvaluator
using AgentEval.Evals;                         // CompositeEval
using AgentEval.Evals.Agentic.Safety.Policy;   // IPolicyResolver (for the Safety category)

namespace AgentEvalMafEvals.Evaluation;

/// <summary>One selectable Agentic benchmark (a tier or a specific preset) and how to build its composite.</summary>
public sealed record BenchmarkChoice(
    string Key,
    string Name,
    string Blurb,
    Func<ChatClientEvaluator, string, CompositeEval> Build);

/// <summary>
/// The Agentic benchmarks offered in the menu. The first three are the <b>smoke / standard /
/// audit-grade</b> tiers, mapped exactly the way AgentEval's own B3 sample does it
/// (Smoke → ToolCallAccuracy, Standard &amp; Audit-Grade → AgenticExecution). The rest are specific
/// presets. (The "Full suite" is handled separately — see <see cref="FullSuiteCategories"/>.)
/// </summary>
public static class BenchmarkCatalog
{
    public static readonly IReadOnlyList<BenchmarkChoice> All =
    [
        // ── Tiers (smoke / standard / audit-grade) ──────────────────────────────
        new("smoke", "Smoke", "Tool-Call Accuracy — 5 sub-evals (fast)",
            (judge, model) => AgenticBenchmark.ToolCallAccuracy(judge, model)),
        new("standard", "Standard", "Agentic Execution — 6 sub-evals (task / intent / tools / navigation)",
            (judge, model) => AgenticBenchmark.AgenticExecution(judge, model)),
        new("audit-grade", "Audit-Grade", "Agentic Execution — same suite as Standard; pair with a stronger judge in production",
            (judge, model) => AgenticBenchmark.AgenticExecution(judge, model)),

        // ── Specific presets (one category each) ────────────────────────────────
        new("reasoning", "Reasoning", "4 sub-evals — reasoning quality",
            (judge, model) => AgenticBenchmark.Reasoning(judge, model)),
        new("user-experience", "User Experience", "5 sub-evals — tone, verbosity, refusal, calibration",
            (judge, model) => AgenticBenchmark.UserExperience(judge, model)),
        new("adversarial-direct", "Adversarial (direct)", "3 sub-evals — injection / persona / jailbreak resistance",
            (judge, model) => AgenticBenchmark.AdversarialDirect(judge, model)),
        new("rag-quality", "RAG Quality", "7 sub-evals — faithfulness, relevance, context (best with retrieval context)",
            (judge, model) => AgenticBenchmark.RagQuality(judge, model)),
    ];

    /// <summary>
    /// The categories that make up the <b>full single-response</b> Agentic run — every category that's
    /// meaningful from one agent answer. The caller runs + reports each one <i>separately</i>, so a
    /// content-filter on one category is attributed and skipped rather than aborting the whole run.
    /// <para>
    /// Deliberately omitted (they need richer inputs a one-shot prompt can't produce): Operational /
    /// Telemetry + Efficiency (runtime latency/cost/token metrics), Memory + Multi-turn (a multi-turn
    /// conversation), and Judge-Quality (a calibration corpus of prior judge results).
    /// </para>
    /// </summary>
    public static IReadOnlyList<(string Name, CompositeEval Composite)> FullSuiteCategories(
        ChatClientEvaluator judge, string model, IPolicyResolver policy) =>
    [
        ("System & Process", AgenticBenchmark.AgenticExecution(judge, model)),
        ("Reasoning", AgenticBenchmark.Reasoning(judge, model)),
        ("UX & Calibration", AgenticBenchmark.UserExperience(judge, model)),
        ("Adversarial", AgenticBenchmark.AdversarialDirect(judge, model)),
        ("RAG Quality", AgenticBenchmark.RagQuality(judge, model)),
        ("Safety", AgenticBenchmark.Safety(judge, policy, subjectId: "TravelAgent", judgeModel: model)),
    ];

    /// <summary>The default tier (Smoke) — used non-interactively.</summary>
    public static BenchmarkChoice Default => All[0];

    public static BenchmarkChoice? ByKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        // Accept the underlying preset names as aliases for the tiers.
        var normalized = key.Trim().ToLowerInvariant() switch
        {
            "tool-call-accuracy" => "smoke",
            "agentic-execution" => "standard",
            var k => k,
        };
        return All.FirstOrDefault(b => string.Equals(b.Key, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public static string Keys => string.Join(", ", All.Select(b => b.Key)) + ", full";
}
