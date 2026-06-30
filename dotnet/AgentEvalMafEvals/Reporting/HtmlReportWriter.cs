// SPDX-License-Identifier: MIT
// AgentEval × MAF — renders an EvalResult tree to a self-contained HTML report.

using System.Diagnostics;
using System.Reflection;
using AgentEval.Core.Evals.Rendering;
using AgentEval.Evals;
using AgentEval.Output;

namespace AgentEvalMafEvals.Reporting;

/// <summary>
/// Turns an AgentEval <see cref="EvalResult"/> tree into a self-contained HTML report (inline CSS,
/// no JS, no CDN) via <see cref="HtmlEvalResultRenderer"/>, writes it under <c>output/</c>, and can
/// open it in the default browser. Rendering + persistence is this class's only responsibility.
/// </summary>
public sealed class HtmlReportWriter
{
    private static readonly string s_agentEvalVersion = ResolveAgentEvalVersion();

    private readonly string _outputDir;
    private readonly SubjectIdentity _subject;

    public HtmlReportWriter(string outputDir, SubjectIdentity subject)
    {
        _outputDir = outputDir;
        _subject = subject;
        Directory.CreateDirectory(_outputDir);
    }

    public async Task<string> WriteAsync(EvalResult tree, string title, string fileTag, bool open)
    {
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(_outputDir, $"agenteval-{fileTag}-{stamp}.html");

        var options = new EvalResultRenderOptions(
            Subject: _subject,
            Title: title,
            RegulationOrBenchmark: "AgentEval via MAF native IAgentEvaluator (agent.EvaluateAsync)",
            GeneratedAt: DateTimeOffset.UtcNow,
            AgentEvalVersion: s_agentEvalVersion);

        var bytes = await new HtmlEvalResultRenderer().RenderAsync(tree, options);
        await File.WriteAllBytesAsync(path, bytes);

        if (open) TryOpen(path);
        return path;
    }

    private static void TryOpen(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { /* headless / CI — the caller prints the path, so opening is best-effort. */ }
    }

    private static string ResolveAgentEvalVersion()
    {
        var asm = typeof(EvalResult).Assembly;
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(info)) return asm.GetName().Version?.ToString() ?? "unknown";
        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }
}
