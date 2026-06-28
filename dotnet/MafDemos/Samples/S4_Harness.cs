// SPDX-License-Identifier: MIT
// Sample 4 — The harness approval gate.
//
// A risky shell tool that requires a human "yes" before it runs. The tool is wrapped in
// ApprovalRequiredAIFunction, so the agent pauses and surfaces a ToolApprovalRequestContent
// instead of executing. You approve (or deny), and the run continues on the same session.
//
// SAFETY: the shell tool here is SIMULATED — it never executes real commands. In production,
// run it inside an isolated environment (container / VM / sandbox) and keep approvals on.

using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using MafDemos.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MafDemos.Samples;

public static class S4_Harness
{
    [Description("Run a bash command on the host and return its stdout. Potentially destructive.")]
    private static string RunBash([Description("The bash command to run")] string command)
        // SIMULATED for the demo — reports success without executing. Replace with real
        // execution inside a sandbox (container / VM) in production.
        => $"[sandbox · simulated] command completed, exit code 0:\n$ {command}";

    public static async Task RunAsync(IChatClient chat)
    {
        Ui.Banner("Sample 4 · The harness approval gate",
            "A risky shell tool that needs a human \"yes\" before it runs");

        // Wrap the tool so the framework pauses for approval before every invocation.
        var gatedBash = new ApprovalRequiredAIFunction(AIFunctionFactory.Create(RunBash, name: "run_bash"));

        AIAgent ops = chat.AsAIAgent(
            name: "Ops",
            instructions: "You are an operations assistant. Use the run_bash tool to accomplish tasks. " +
                          "Prefer a single, specific command. Avoid destructive commands.",
            tools: [gatedBash]);

        var task = Ui.Prompt("ask Ops to do something (or Enter for the default)");
        if (string.IsNullOrWhiteSpace(task))
            task = "Clean up old *.log files in the system temp folder.";
        Ui.KeyValue("Task", $"\"{task}\"");

        AgentSession session = await ops.CreateSessionAsync();

        AgentResponse response;
        using (new Ui.Spinner("Ops is deciding what to run…"))
            response = await ops.RunAsync(task, session);

        var approvalRequests = PendingApprovals(response);

        if (approvalRequests.Count == 0)
            Ui.Dim("(The agent answered without requesting any gated tool.)");

        while (approvalRequests.Count > 0)
        {
            var responses = new List<ChatMessage>();
            foreach (var req in approvalRequests)
            {
                PrintApprovalGate(req);
                var approved = Ui.Confirm($"approve {ToolName(req)}?");
                if (approved) Ui.Success("approved — the tool will run");
                else Ui.Failure("denied — the tool will be skipped");
                responses.Add(new ChatMessage(ChatRole.User, [req.CreateResponse(approved)]));
            }

            using (new Ui.Spinner("continuing the run…"))
                response = await ops.RunAsync(responses, session);

            approvalRequests = PendingApprovals(response);
        }

        Ui.Rule("result");
        Ui.Write("  [Ops] ", Ui.ColorFor("Ops"));
        Ui.Line((response.Text ?? "").Trim(), ConsoleColor.White);

        Ui.Rule();
        Ui.Hint("The gate is the wrapper: ApprovalRequiredAIFunction makes the agent emit a " +
                "ToolApprovalRequestContent and wait. Unwrap it and the command would run unattended.");
        Ui.PressAnyKey();
    }

    private static List<ToolApprovalRequestContent> PendingApprovals(AgentResponse response) =>
        response.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();

    // The approval request carries the pending tool call as a ToolCallContent; cast to
    // FunctionCallContent to read the function name and arguments.
    private static string ToolName(ToolApprovalRequestContent req) =>
        (req.ToolCall as FunctionCallContent)?.Name ?? req.ToolCall.CallId;

    private static void PrintApprovalGate(ToolApprovalRequestContent req)
    {
        var call = req.ToolCall as FunctionCallContent;
        var args = call?.Arguments is { Count: > 0 } a ? JsonSerializer.Serialize(a, s_argsJson) : "{}";
        Console.WriteLine();
        Ui.Line("  ┌─ approval required ─────────────────────────────────────────", Ui.Warn);
        Ui.Write("  │ tool  ", Ui.Warn); Ui.Line(ToolName(req), ConsoleColor.White);
        Ui.Write("  │ args  ", Ui.Warn); Ui.Line(Truncate(args, 56), ConsoleColor.Gray);
        Ui.Line("  └─────────────────────────────────────────────────────────────", Ui.Warn);
    }

    private static readonly JsonSerializerOptions s_argsJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static string Truncate(string s, int n) => s.Length <= n ? s : s[..n] + "…";
}
