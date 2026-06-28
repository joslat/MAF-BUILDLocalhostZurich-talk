// SPDX-License-Identifier: MIT
// Sample 5 — ShopBot (all three pillars), the "build this first" finale.
//
// One small app that:
//   • cooperates  — a Triage→Orders/Returns→Fraud handoff graph (Sample 1)
//   • acts        — an approval-gated issue_refund tool (Sample 4)
//   • is tested   — a closing behavior eval on the Returns build (Sample 2)
//
// It's Samples 1 + 4 + 2 on a single shared cast, shown as three short acts.

using System.ComponentModel;
using MafDemos.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MafDemos.Samples;

public static class S5_ShopBot
{
    [Description("Look up an order by id and return a short status line.")]
    private static string LookupOrder([Description("The order id, e.g. 1234")] string orderId)
        => $"Order {orderId}: 1x Blue Mug, $18.00 — delivered on Tuesday.";

    [Description("Issue a refund for an order and return a confirmation.")]
    private static string IssueRefund([Description("The order id, e.g. 1234")] string orderId)
        => $"Refund of $18.00 issued for order {orderId}.";

    public static async Task RunAsync(IChatClient chat)
    {
        Ui.Banner("Sample 5 · ShopBot — the finale",
            "Cooperate (handoff) · Act (approval-gated refund) · Tested (eval)");

        var lookup = AIFunctionFactory.Create(LookupOrder, name: "lookup_order");
        var refund = AIFunctionFactory.Create(IssueRefund, name: "issue_refund");

        // ── Act I — COOPERATE: a handoff graph routes the customer ───────────────────────
        Ui.Rule("act I · cooperate — the handoff graph");

        ChatClientAgent triage = new(chat,
            "You are the front desk. Route the customer to Orders or Returns and hand off. Don't answer yourself.",
            "Triage", "Routes the customer to Orders or Returns");
        // Orders/Returns get the read-only lookup tool inside the workflow (safe, auto-invoked).
        ChatClientAgent orders = new(chat,
            "You answer order-status questions. Use lookup_order to get the facts, then reply briefly.",
            "Orders", "Handles order-status questions", [lookup]);
        ChatClientAgent returns = new(chat,
            "You handle returns and refunds. Use lookup_order to verify the order. Escalate abuse to Fraud.",
            "Returns", "Handles returns and refunds", [lookup]);
        ChatClientAgent fraud = new(chat,
            "You handle suspected fraud. Say the case is escalated. This is the end of the line.",
            "Fraud", "Suspected fraud — terminal");

        Workflow shop = AgentWorkflowBuilder.CreateHandoffBuilderWith(triage)
            .WithHandoffs(triage, [orders, returns])
            .WithHandoff(returns, fraud, handoffReason: "Suspected fraud or abuse.")
            .WithHandoffs([orders, returns], triage)
            .Build();

        const string question = "Where is my order #1234?";
        Ui.KeyValue("Customer", $"\"{question}\"");
        await RunTurnAsync(shop, [new ChatMessage(ChatRole.User, question)]);

        // ── Act II — ACT: an approval-gated refund ───────────────────────────────────────
        Ui.Rule("act II · act — the approval gate");

        var gatedRefund = new ApprovalRequiredAIFunction(refund);
        AIAgent returnsDesk = chat.AsAIAgent(
            name: "Returns",
            instructions: "You process refunds. When asked for a refund, call issue_refund with the order id.",
            tools: [lookup, gatedRefund]);

        const string refundAsk = "My mug arrived smashed — please refund order #1234.";
        Ui.KeyValue("Customer", $"\"{refundAsk}\"");

        AgentSession session = await returnsDesk.CreateSessionAsync();
        AgentResponse resp;
        using (new Ui.Spinner("Returns is working…"))
            resp = await returnsDesk.RunAsync(refundAsk, session);

        var approvals = resp.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
        while (approvals.Count > 0)
        {
            var replies = new List<ChatMessage>();
            foreach (var req in approvals)
            {
                var toolName = (req.ToolCall as FunctionCallContent)?.Name ?? req.ToolCall.CallId;
                Console.WriteLine();
                Ui.Line($"  ┌─ approval required · {toolName} ─────────────", Ui.Warn);
                Ui.Line("  └ a refund moves real money — that's why it's gated", Ui.Warn);
                var ok = Ui.Confirm($"approve {toolName}?");
                if (ok) Ui.Success("approved"); else Ui.Failure("denied");
                replies.Add(new ChatMessage(ChatRole.User, [req.CreateResponse(ok)]));
            }
            using (new Ui.Spinner("continuing…"))
                resp = await returnsDesk.RunAsync(replies, session);
            approvals = resp.Messages.SelectMany(m => m.Contents).OfType<ToolApprovalRequestContent>().ToList();
        }

        Ui.Write("  [Returns] ", Ui.ColorFor("Returns"));
        Ui.Line((resp.Text ?? "").Trim(), ConsoleColor.White);

        // ── Act III — TESTED: closing behavior eval ──────────────────────────────────────
        Ui.Rule("act III · tested — the closing eval");

        AIAgent evalReturns = chat.AsAIAgent(
            name: "Returns",
            instructions: "When asked for a refund, you MUST call issue_refund with the order id.",
            tools: [refund]);

        AgentResponse evalResp;
        using (new Ui.Spinner("running behavior eval…"))
            evalResp = await evalReturns.RunAsync("Refund my order #1234.");

        var calledRefund = evalResp.Messages.SelectMany(m => m.Contents)
            .OfType<FunctionCallContent>()
            .Any(c => c.Name.Equals("issue_refund", StringComparison.OrdinalIgnoreCase));

        Ui.Badge(calledRefund, "Returns build issues a refund when asked  (issue_refund called)");

        Ui.Rule();
        Ui.Hint("That's the whole story: agents that cooperate, act under human approval, and are " +
                "tested for the behavior you care about — one small app, three pillars.");
        Ui.PressAnyKey();
    }

    // Compact streaming turn runner (same robust shape as Sample 1, plus tool-call lines).
    private static async Task RunTurnAsync(Workflow workflow, List<ChatMessage> conversation)
    {
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, conversation);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        var stream = new Ui.AgentStream();
        string? lastSpeaker = null;
        string? handoffPendingFrom = null;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent e)
            {
                var speaker = e.Update.AuthorName is { Length: > 0 } an ? an : e.ExecutorId;

                foreach (var call in e.Update.Contents.OfType<FunctionCallContent>())
                {
                    if (call.Name.StartsWith("handoff_to_", StringComparison.OrdinalIgnoreCase))
                    {
                        handoffPendingFrom = speaker;
                    }
                    else
                    {
                        stream.End();
                        stream.Reset(); // reprint the speaker label after the tool line
                        Ui.Write("  ⚙ ", Ui.Muted);
                        Ui.Line($"{speaker} calls {call.Name}", Ui.Muted);
                    }
                }

                if (speaker != lastSpeaker)
                {
                    if (handoffPendingFrom is { } from && from != speaker)
                    {
                        stream.End();
                        Ui.Write("  ⇢ ", Ui.Muted);
                        Ui.Write(from, Ui.ColorFor(from));
                        Ui.Write("  hands off to  ", Ui.Muted);
                        Ui.Line(speaker, Ui.ColorFor(speaker));
                        handoffPendingFrom = null;
                    }
                    lastSpeaker = speaker;
                }

                stream.Chunk(speaker, e.Update.Text);
            }
            else if (evt is WorkflowOutputEvent)
            {
                stream.End();
                return;
            }
            else if (evt is WorkflowErrorEvent err)
            {
                stream.End();
                Ui.Failure(err.Exception?.Message ?? "workflow error");
                return;
            }
        }
        stream.End();
    }
}
