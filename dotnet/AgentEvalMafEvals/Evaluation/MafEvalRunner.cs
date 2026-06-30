// SPDX-License-Identifier: MIT
// AgentEval × MAF — runs AgentEval evaluations through MAF's NATIVE IAgentEvaluator.

using AgentEval.Evals;                  // EvalResult, CompositeEval
using AgentEval.MAF.Evaluators;         // AgentEvalEvaluators, AsAgentEvaluator, AsMeaiEvaluator, MeaiToEvalResultBridge
using AgentEval.Metrics.Agentic;        // ToolSuccessMetric, ToolSelectionMetric, TaskCompletionMetric
using AgentEval.Metrics.RAG;            // RelevanceMetric
using AgentEval.Metrics.Safety;         // CoherenceMetric
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;

namespace AgentEvalMafEvals.Evaluation;

/// <summary>
/// Runs AgentEval evaluations through MAF's <b>native</b> <c>IAgentEvaluator</c> overload —
/// <c>agent.EvaluateAsync(queries, evaluator)</c>. Nothing here uses the MEAI <c>IEvaluator</c>
/// overload: the native path hands the evaluator the full <c>EvalItem.Conversation</c>, so even the
/// code-based tool metrics see the real tool calls.
/// </summary>
public sealed class MafEvalRunner(IChatClient judge, ChatConfiguration judgeConfig, string judgeModel)
{
    /// <summary>Flat metrics (tool + quality) bundled as one native IAgentEvaluator.</summary>
    public async Task<EvalRun> RunFlatAsync(AIAgent agent, string[] queries)
    {
        var metrics = AgentEvalEvaluators.Custom(
            new ToolSuccessMetric(),
            new ToolSelectionMetric([Tools.TravelTools.SearchFlightsName, Tools.TravelTools.SearchHotelsName]),
            new TaskCompletionMetric(judge),
            new RelevanceMetric(judge),
            new CoherenceMetric(judge));

        var evaluator = metrics.AsAgentEvaluator(judgeConfig, "AgentEval-Flat");
        AgentEvaluationResults results = await agent.EvaluateAsync(queries, evaluator);

        var tree = MeaiToEvalResultBridge.Build("AgentEval × MAF — Flat Metrics", queries, results, judgeModel);
        var verdict = $"MAF {results.Passed}/{results.Total} passed (AllPassed = {results.AllPassed})";
        return new EvalRun("Flat metrics", "flat", tree, verdict);
    }

    /// <summary>
    /// Runs a chosen AgenticBenchmark composite (weighted tree + thresholds) as one native
    /// IAgentEvaluator. The caller builds <paramref name="composite"/> from <see cref="BenchmarkCatalog"/>.
    /// </summary>
    public async Task<EvalRun> RunCompositeAsync(AIAgent agent, string[] queries, CompositeEval composite)
    {
        var compositeEvaluator = composite.AsMeaiEvaluator();             // IEval -> MEAI IEvaluator (captures the tree)
        var evaluator = compositeEvaluator.AsAgentEvaluator(judgeConfig, "AgentEval-Composite");

        AgentEvaluationResults results = await agent.EvaluateAsync(queries, evaluator);

        var tree = compositeEvaluator.CapturedResults[0];                 // the full weighted hierarchy
        var verdict = $"MAF {results.Passed}/{results.Total} passed (AllPassed = {results.AllPassed})";
        return new EvalRun($"Composite · {composite.Name}", "composite", tree, verdict);
    }
}
