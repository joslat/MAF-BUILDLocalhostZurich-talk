// SPDX-License-Identifier: MIT
// AgentEval × MAF — the red-team benchmark families (OWASP / MITRE / NIST) and their tiers.

using AgentEval.Benchmarks;     // OwaspBenchmark, MitreBenchmark, NistBenchmark
using AgentEval.Core;           // IEvaluator, IEvaluableAgent
using AgentEval.Evals;          // EvalResult

namespace AgentEvalMafEvals.Evaluation;

/// <summary>Preset tier — maps to each family's Smoke / Standard / Audit-Grade factory.</summary>
public enum Tier { Smoke, Standard, AuditGrade }

/// <summary>A built red-team run: its preset name, the IDs it covers, and a scan→EvalResult function.</summary>
public sealed record RedTeamRunHandle(
    string PresetName,
    IReadOnlyList<string> CoveredIds,
    Func<IEvaluableAgent, CancellationToken, Task<EvalResult>> ScanToEvalResultAsync);

/// <summary>A red-team family and how to build it at a given <see cref="Tier"/>.</summary>
public sealed record RedTeamFamily(string Key, string Name, string Blurb, Func<IEvaluator?, Tier, RedTeamRunHandle> Build);

/// <summary>
/// The red-team families. All three share one shape — a factory per tier returning a run with
/// <c>PresetName</c>, a covered-IDs list, <c>ScanAsync(IEvaluableAgent)</c>, and
/// <c>BuildEvalResult(...)</c> — so we compose them behind a uniform <see cref="RedTeamRunHandle"/>.
/// </summary>
public static class RedTeamCatalog
{
    public static readonly IReadOnlyList<RedTeamFamily> Families =
    [
        new("owasp", "OWASP LLM Top 10", "13 attack types across the OWASP LLM Top 10", (judge, tier) =>
        {
            var run = tier switch
            {
                Tier.Standard => OwaspBenchmark.Top10(judge),
                Tier.AuditGrade => OwaspBenchmark.AuditGrade(judge),
                _ => OwaspBenchmark.Smoke(judge),
            };
            return new RedTeamRunHandle(run.PresetName, run.CoveredOwaspIds,
                async (agent, ct) => run.BuildEvalResult(await run.ScanAsync(agent, ct)));
        }),
        new("mitre", "MITRE ATLAS", "ATLAS technique-level adversarial probes", (judge, tier) =>
        {
            var run = tier switch
            {
                Tier.Standard => MitreBenchmark.AtlasBaseline(judge),
                Tier.AuditGrade => MitreBenchmark.AtlasAuditGrade(judge),
                _ => MitreBenchmark.AtlasSmoke(judge),
            };
            return new RedTeamRunHandle(run.PresetName, run.CoveredAtlasIds,
                async (agent, ct) => run.BuildEvalResult(await run.ScanAsync(agent, ct)));
        }),
        new("nist", "NIST AI RMF", "MEASURE security / privacy / validity evidence", (judge, tier) =>
        {
            var run = tier switch
            {
                Tier.Standard => NistBenchmark.RmfBaseline(judge),
                Tier.AuditGrade => NistBenchmark.RmfAuditGrade(judge),
                _ => NistBenchmark.RmfSmoke(judge),
            };
            return new RedTeamRunHandle(run.PresetName, run.CoveredControlIds,
                async (agent, ct) => run.BuildEvalResult(await run.ScanAsync(agent, ct)));
        }),
    ];

    public static readonly IReadOnlyList<(Tier Tier, string Name, string Blurb)> Tiers =
    [
        (Tier.Smoke, "Smoke", "fast — a few probes per attack"),
        (Tier.Standard, "Standard", "the full top-10 / baseline coverage"),
        (Tier.AuditGrade, "Audit-Grade", "thorough — comprehensive intensity (slower, costs more)"),
    ];
}
