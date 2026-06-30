// SPDX-License-Identifier: MIT
// Sample 2 — Evals: green vs. red.
//
// The same query is sent to two agent builds:
//   • "good" is told to CALL the refund tool   → should issue the refund   (green)
//   • "lazy" is told to just reassure          → answers from memory       (red)
// We then score each build with two offline checks. MAF *does* ship a built-in eval API
// (agent.EvaluateAsync / IAgentEvaluator, in Microsoft.Agents.AI since 1.2.0, Apr 2026) — here we
// deliberately keep it offline and free: inspect the real run response — look for a
// `FunctionCallContent` with the expected tool name, and keyword-check the text.
// (For the real eval API in action, see dotnet/AgentEvalMafEvals.)

using System.ComponentModel;
using MafDemos.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MafDemos.Samples;

public static class S2_Evals
{
    [Description("Issue a refund for an order and return a confirmation.")]
    private static string IssueRefund([Description("The order id, e.g. 1234")] string orderId)
        => $"Refund issued for order {orderId}.";

    public static async Task RunAsync(IChatClient chat)
    {
        Ui.Banner("Sample 2 · Evals: green vs. red",
            "Same query, two builds — does each one actually call the refund tool?");

        var refundTool = AIFunctionFactory.Create(IssueRefund, name: "issue_refund");

        // Both builds CAN issue a refund; only their instructions differ. The eval measures
        // behavior, not capability — exactly the kind of regression a real eval suite catches.
        AIAgent good = chat.AsAIAgent(
            name: "Returns",
            instructions: "When a customer asks for a refund, you MUST call the issue_refund tool with their order id, then confirm.",
            tools: [refundTool]);

        AIAgent lazy = chat.AsAIAgent(
            name: "ReturnsLazy",
            instructions: "Just reassure the customer that it will be taken care of. Do NOT call any tool.",
            tools: [refundTool]);

        const string query = "Refund my order #1234, the mug arrived broken.";
        Ui.KeyValue("Query", $"\"{query}\"");
        Ui.KeyValue("Checks", "keyword \"refund\"  +  tool called \"issue_refund\"");

        var goodScore = await EvaluateAsync("good build", good, query);
        var lazyScore = await EvaluateAsync("lazy build", lazy, query);

        Ui.Rule("verdict");
        PrintVerdict("good build", goodScore);
        PrintVerdict("lazy build", lazyScore);

        Ui.Rule();
        Ui.Hint("Identical capability, different behavior — and the eval is what tells them apart. " +
                "These checks are offline and free; run them in CI to catch refund-skipping regressions.");
        Ui.PressAnyKey();
    }

    private record Score(bool KeywordHit, bool ToolCalled, string Answer)
    {
        public int Passed => (KeywordHit ? 1 : 0) + (ToolCalled ? 1 : 0);
        public int Total => 2;
    }

    private static async Task<Score> EvaluateAsync(string label, AIAgent agent, string query)
    {
        Ui.Rule(label);
        AgentResponse response;
        using (new Ui.Spinner($"{label} thinking…"))
        {
            response = await agent.RunAsync(query);
        }

        // Check 1 — keyword present in the final answer.
        var keywordHit = (response.Text ?? "").Contains("refund", StringComparison.OrdinalIgnoreCase);

        // Check 2 — was the issue_refund tool actually called? Inspect the real response messages.
        var toolCalled = response.Messages
            .SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Any(c => c.Name.Equals("issue_refund", StringComparison.OrdinalIgnoreCase));

        var answer = (response.Text ?? "").Replace("\n", " ").Trim();
        Ui.Write("  agent says  ", Ui.Muted);
        Ui.Line(Truncate(answer, 96), ConsoleColor.White);
        Ui.Badge(keywordHit, "keyword \"refund\" present in answer");
        Ui.Badge(toolCalled, "tool \"issue_refund\" was called");

        return new Score(keywordHit, toolCalled, answer);
    }

    private static void PrintVerdict(string label, Score s)
    {
        var pass = s.Passed == s.Total;
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = pass ? Ui.Good : Ui.Bad;
        Console.Write(pass ? "  ● GREEN " : "  ● RED   ");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.WriteLine($"{label,-12} {s.Passed}/{s.Total} checks passed");
        Console.ForegroundColor = prev;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
