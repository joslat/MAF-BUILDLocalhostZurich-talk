// SPDX-License-Identifier: MIT
// AgentEval × MAF — the product of one evaluation run.

using AgentEval.Evals;

namespace AgentEvalMafEvals.Evaluation;

/// <summary>
/// The result of one evaluation: a short human verdict line plus AgentEval's rich
/// <see cref="EvalResult"/> tree (rendered to HTML). Shared by the metric, Agentic-benchmark, and
/// red-team paths so the reporter can treat them uniformly.
/// </summary>
public sealed record EvalRun(string Title, string FileTag, EvalResult Tree, string Verdict);
