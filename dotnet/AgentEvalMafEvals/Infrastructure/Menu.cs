// SPDX-License-Identifier: MIT
// AgentEval × MAF — a tiny reusable single-choice menu.

namespace AgentEvalMafEvals.Infrastructure;

/// <summary>A reusable numbered menu. Composes the main menu and every submenu.</summary>
public static class Menu
{
    /// <summary>
    /// Prints <paramref name="title"/> and the numbered <paramref name="options"/> (plus a back/quit
    /// row), reads a choice, and returns the 0-based index — or <c>null</c> for back/quit
    /// (<c>b</c>, <c>q</c>, or Enter). Re-prompts on invalid input.
    /// </summary>
    public static int? Choose(
        string title,
        IReadOnlyList<(string Label, string Desc)> options,
        string backLabel = "Back")
    {
        while (true)
        {
            Output.Section(title);
            for (var i = 0; i < options.Count; i++)
                Output.Option($"[{i + 1}] {options[i].Label}", options[i].Desc);
            Output.Option($"[b] {backLabel}", "");

            Console.Write("\n  Choice: ");
            var raw = (Console.ReadLine() ?? "b").Trim().ToLowerInvariant();

            if (raw.Length == 0 || raw is "b" or "q")
                return null;
            if (int.TryParse(raw, out var n) && n >= 1 && n <= options.Count)
                return n - 1;

            Console.WriteLine($"  Enter 1-{options.Count}, or b.");
        }
    }
}
