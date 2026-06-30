// SPDX-License-Identifier: MIT
// AgentEval × MAF — parses the optional --benchmark <key> argument.

namespace AgentEvalMafEvals.Evaluation;

/// <summary>Resolves an explicit <c>--benchmark &lt;key&gt;</c> argument (the interactive picker lives in the menu loop).</summary>
public static class BenchmarkSelector
{
    /// <summary>Returns the <c>--benchmark &lt;key&gt;</c> choice, or <c>null</c> when not specified.</summary>
    public static BenchmarkChoice? FromArgs(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], "--benchmark", StringComparison.OrdinalIgnoreCase))
                continue;

            // "full" is handled separately (the full-suite run), not a single BenchmarkChoice.
            if (string.Equals(args[i + 1], "full", StringComparison.OrdinalIgnoreCase))
                return null;

            var byKey = BenchmarkCatalog.ByKey(args[i + 1]);
            if (byKey is not null)
                return byKey;

            Console.WriteLine($"  Unknown --benchmark '{args[i + 1]}'. Known: {BenchmarkCatalog.Keys}");
        }

        return null;
    }
}
