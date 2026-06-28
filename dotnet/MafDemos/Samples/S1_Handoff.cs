// SPDX-License-Identifier: MIT
// Sample 1 — Handoff you can watch.
//
// A Triage agent routes the customer to Orders or Returns; Returns can escalate one-way to a
// terminal Fraud agent. Every hop is printed live. Built with the real MAF handoff workflow:
//   AgentWorkflowBuilder.CreateHandoffBuilderWith(...).WithHandoffs(...).Build()
// driven by an InProcess streaming run (StreamAsync → TurnToken → WatchStreamAsync).

using MafDemos.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace MafDemos.Samples;

public static class S1_Handoff
{
    public static async Task RunAsync(IChatClient chat)
    {
        Ui.Banner("Sample 1 · Handoff you can watch",
            "Triage → Orders / Returns · Returns ⇢ Fraud (one-way, terminal)");

        // ── The cast: four agents. name drives the colored label; description becomes the
        //    handoff reason the model sees when deciding where to route. ──────────────────
        ChatClientAgent triage = new(chat,
            instructions: "You are the front desk. Route the customer to the right specialist and " +
                          "hand off immediately. Do not answer order or return questions yourself.",
            name: "Triage", description: "Routes the customer to Orders or Returns");

        ChatClientAgent orders = new(chat,
            instructions: "You answer order-status questions concisely and politely.",
            name: "Orders", description: "Handles order-status questions");

        ChatClientAgent returns = new(chat,
            instructions: "You handle returns and refunds. If a request looks like abuse or fraud " +
                          "(many recent refunds, contradictory claims), hand off to Fraud.",
            name: "Returns", description: "Handles returns and refunds");

        ChatClientAgent fraud = new(chat,
            instructions: "You handle suspected fraud. State that the case is escalated for review. " +
                          "This is the end of the line — do not hand off.",
            name: "Fraud", description: "Handles suspected fraud or abuse — terminal");

        // ── The graph: edges declare what's allowed. No edge = no handoff. ───────────────
        Workflow workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triage)
            .WithHandoffs(triage, [orders, returns])                       // triage → specialists
            .WithHandoff(returns, fraud, handoffReason: "Suspected return fraud or abuse.") // one-way escalation
            .WithHandoffs([orders, returns], triage)                       // let specialists hand back
            .Build();

        Ui.Rule("conversation");
        Ui.Dim("Try:  \"Where is my order #1234?\"   ·   \"My mug arrived smashed, refund order #1234\"");
        Ui.Dim("      \"This is my 9th refund this week, give me my money\"   ·   type 'exit' to finish");

        List<ChatMessage> conversation = [];
        while (true)
        {
            var input = Ui.Prompt("customer");
            if (input is null) break;             // EOF → finish
            if (string.IsNullOrWhiteSpace(input)) continue;
            if (input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

            conversation.Add(new ChatMessage(ChatRole.User, input));
            var produced = await RunTurnAsync(workflow, conversation);
            // The workflow output carries the FULL updated thread (not just new messages),
            // so replace the history rather than appending — otherwise it duplicates each turn.
            if (produced.Count > 0) conversation = produced;
        }

        Ui.Rule();
        Ui.Hint("Handoffs are just tool calls: the model invokes a `handoff_to_<agent>` function the " +
                "builder injected. No edge = no route — that's why Fraud can't hand back.");
        Ui.PressAnyKey();
    }

    /// <summary>Runs one workflow turn, streaming output grouped by the speaking agent and
    /// printing every handoff hop as it happens.</summary>
    private static async Task<List<ChatMessage>> RunTurnAsync(Workflow workflow, List<ChatMessage> conversation)
    {
        await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, conversation);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        var stream = new Ui.AgentStream();
        string? lastSpeaker = null;
        string? handoffPendingFrom = null;

        await foreach (WorkflowEvent evt in run.WatchStreamAsync())
        {
            switch (evt)
            {
                case AgentResponseUpdateEvent e:
                {
                    var speaker = e.Update.AuthorName is { Length: > 0 } an ? an : e.ExecutorId;

                    // A handoff is the model calling a `handoff_to_*` function the builder injected.
                    if (e.Update.Contents.OfType<FunctionCallContent>()
                            .Any(c => c.Name.StartsWith("handoff_to_", StringComparison.OrdinalIgnoreCase)))
                        handoffPendingFrom = speaker;

                    // Announce the hop when the next agent actually takes over — using its real name.
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
                    break;
                }

                case WorkflowOutputEvent output:
                    stream.End();
                    return output.As<List<ChatMessage>>() ?? [];

                case WorkflowErrorEvent err:
                    stream.End();
                    Ui.Failure(err.Exception?.Message ?? "workflow error");
                    return [];
            }
        }

        stream.End();
        return [];
    }
}
