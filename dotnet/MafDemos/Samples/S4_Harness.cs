// SPDX-License-Identifier: MIT
// Sample 4 — A real Agent Harness.
//
// MAF's *agent harness* (Microsoft.Agents.AI.Harness) turns any IChatClient into a HarnessAgent:
// a pre-configured agentic loop with planning (a persistent todo list via TodoProvider), plan/execute
// modes (AgentModeProvider), in-loop context compaction, and built-in tool approval. It's the
// foundation the "build your own claw" coding-agent samples are made of — `chatClient.AsHarnessAgent(...)`.
//
// Here that harness gets three travel tools and a multi-step task. Watch it PLAN (todos_add), switch
// to execute mode, work the tools to find the best flight + hotel, then PAUSE and ask before it spends
// money. You approve with a single Enter, and it books. The human checkpoint is a natural turn in the
// conversation, so it resumes cleanly; the official samples wrap this same HarnessAgent in a full
// reactive HarnessConsole UI (whose interactive runner injects approvals mid-run).
//
// SAFETY: the "book" tool is SIMULATED — it never charges anything. In production the harness can run
// real shell/file tools; keep tool approval on and sandbox the executor.

#pragma warning disable MAAI001 // AsHarnessAgent / HarnessAgent* are [Experimental] in this preview.

using System.Text;
using System.Text.Json;
using System.ComponentModel;
using MafDemos.Infrastructure;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace MafDemos.Samples;

public static class S4_Harness
{
    [Description("Search flights between two cities on a date. Returns a few options with prices.")]
    private static string SearchFlights(
        [Description("Departure city")] string origin,
        [Description("Destination city")] string destination,
        [Description("Date, e.g. 2026-07-03")] string date)
        => $"FLIGHTS {origin}→{destination} {date}: " +
           "LX110 07:15 €148 · LX140 12:40 €169 · U2884 18:05 €121 (cheapest).";

    [Description("Search hotels in a city for a date range. Returns a few options with nightly rates.")]
    private static string SearchHotels(
        [Description("City")] string city,
        [Description("Check-in date")] string checkIn,
        [Description("Check-out date")] string checkOut)
        => $"HOTELS {city} {checkIn}→{checkOut}: " +
           "Hotel Banys €175/night · Praktik Rambla €140/night (best value) · W Barcelona €420/night.";

    // The risky one — only called after the traveller confirms. SIMULATED: never books or charges.
    [Description("Book the chosen flight + hotel for the trip. Only call this after the traveller confirms.")]
    private static string BookTrip(
        [Description("The chosen flight, e.g. 'U2884'")] string flight,
        [Description("The chosen hotel, e.g. 'Praktik Rambla'")] string hotel)
        => $"[simulated] Booked {flight} + {hotel}. Confirmation TRIP-4827. No card was charged.";

    public static async Task RunAsync(IChatClient chat)
    {
        Ui.Banner("Sample 4 · A real Agent Harness",
            "chatClient.AsHarnessAgent — it plans (todos), executes, then asks before it books");

        // AsHarnessAgent pre-wires function invocation, a TodoProvider (planning), an AgentModeProvider
        // (plan ⇄ execute), and compaction. We add the concierge's brief + three tools. Web search /
        // file access / file memory need a provider or disk — disabled to keep the demo self-contained.
        AIAgent concierge = chat.AsHarnessAgent(new HarnessAgentOptions
        {
            Name = "Concierge",
            ChatOptions = new ChatOptions
            {
                Instructions =
                    "You are a travel concierge harness. For any trip request: FIRST call todos_add to lay " +
                    "out your plan (find a flight, find a hotel, confirm with the traveller, book, write the " +
                    "itinerary). Use search_flights and search_hotels to gather options and pick the BEST " +
                    "VALUE. Then STOP: present your proposed booking — the chosen flight, the hotel, the " +
                    "number of nights, and the total cost — and ask the traveller to confirm. Do NOT call " +
                    "book_trip yet. Only after the traveller confirms in their next message, call book_trip " +
                    "and finish with a tidy itinerary.",
                Tools =
                [
                    AIFunctionFactory.Create(SearchFlights),
                    AIFunctionFactory.Create(SearchHotels),
                    AIFunctionFactory.Create(BookTrip, name: "book_trip"),
                ],
            },
            DisableWebSearch = true,
            DisableFileAccess = true,
            DisableFileMemory = true,
        });

        const string defaultTask =
            "Plan a long weekend in Barcelona for two, flying from Zurich on 2026-07-03, back 2026-07-06. " +
            "Find a flight and a hotel, then book the trip.";

        var typed = Ui.Prompt("give the concierge a task (or just press Enter for the default)");
        var task = string.IsNullOrWhiteSpace(typed) ? defaultTask : typed!;
        Ui.KeyValue("Task", $"\"{task}\"");

        AgentSession session = await concierge.CreateSessionAsync();

        // Turn 1 — the harness plans, searches, and proposes. We stream it (the harness's native path)
        // and accumulate the turn, then render it as a tidy plan/execute timeline.
        List<AIContent> turn;
        string proposal;
        using (new Ui.Spinner("the harness is planning…"))
            (turn, proposal) = await StreamTurnAsync(concierge, [new ChatMessage(ChatRole.User, task)], session);

        Ui.Rule("the harness at work");
        RenderTurn(turn);

        // If the model jumped straight to booking, there's nothing to gate — just show the result.
        if (CalledBookTrip(turn))
        {
            Ui.Rule("itinerary");
            PrintConcierge(proposal);
        }
        else
        {
            // Turn 1 ended with a proposal + a question. Show it, then gate the spend on one Enter.
            Ui.Rule("proposed booking");
            PrintConcierge(proposal);

            bool approved = ApproveByDefault("approve this booking and let the harness reserve it?");
            string reply = approved
                ? "Yes — confirmed. Book it now, then give me the final itinerary."
                : "No — please don't book anything. Cancel the trip.";

            if (approved) Ui.Success("approved — the harness will book it");
            else Ui.Failure("declined — nothing will be booked");

            // Turn 2 — a normal follow-up turn. On "yes" the harness calls book_trip and writes the itinerary.
            string closing;
            using (new Ui.Spinner(approved ? "the harness is booking…" : "the harness is standing down…"))
                (turn, closing) = await StreamTurnAsync(concierge, [new ChatMessage(ChatRole.User, reply)], session);

            RenderTurn(turn);
            Ui.Rule(approved ? "itinerary" : "result");
            PrintConcierge(closing);
        }

        Ui.Rule();
        Ui.Hint("This is the real HarnessAgent (AsHarnessAgent): the todo plan, plan/execute modes, and " +
                "compaction are built in. The 'build your own claw' samples wrap exactly this agent in a " +
                "reactive HarnessConsole UI that also gates risky tools with framework-level approval.");
        Ui.PressAnyKey();
    }

    // ── Drive one turn over the streaming API; collect its content + final text ───────
    private static async Task<(List<AIContent> Content, string Text)> StreamTurnAsync(
        AIAgent agent, IEnumerable<ChatMessage> input, AgentSession session)
    {
        var content = new List<AIContent>();
        var text = new StringBuilder();
        await foreach (var update in agent.RunStreamingAsync(input, session))
        {
            if (update.Contents is { } cs) content.AddRange(cs);
            if (!string.IsNullOrEmpty(update.Text)) text.Append(update.Text);
        }
        return (content, text.ToString());
    }

    private static bool CalledBookTrip(List<AIContent> content) =>
        content.OfType<FunctionCallContent>().Any(c => c.Name == "book_trip");

    private static void PrintConcierge(string text)
    {
        Ui.Write("  [Concierge] ", Ui.ColorFor("Concierge"));
        Ui.Line(string.IsNullOrWhiteSpace(text) ? "(no message)" : text.Trim(), ConsoleColor.White);
    }

    // ── Render one turn's tool activity as a plan/execute timeline ───────────────────
    private static void RenderTurn(List<AIContent> content)
    {
        var seen = new HashSet<string>();
        foreach (var c in content)
            if (c is FunctionCallContent call && seen.Add(call.CallId ?? call.Name))
                RenderCall(call);
    }

    private static void RenderCall(FunctionCallContent call)
    {
        switch (call.Name)
        {
            case "todos_add":
                var plan = StringValues(call, "title");
                if (plan.Count > 0)
                {
                    Ui.Line("  📋 plan", Ui.Accent);
                    for (int i = 0; i < plan.Count; i++)
                        Ui.Line($"      {i + 1}. {plan[i]}", ConsoleColor.White);
                }
                else Ui.Step("📋", "plan");
                break;

            case "todos_complete":
                var reasons = StringValues(call, "reason");
                Ui.Success(reasons.Count > 0 ? $"done — {string.Join("; ", reasons)}" : "step done");
                break;

            case "mode_set":
                Ui.Dim($"⚙ mode → {FirstString(call) ?? "execute"}");
                break;

            // Read-only bookkeeping the harness does — hide it to keep the timeline clean.
            case "todos_get_remaining":
            case "todos_get_all":
            case "mode_get":
                break;

            default: // the concierge's own tools — search_flights / search_hotels / book_trip
                Ui.Step("🔧", $"{call.Name}({Args(call)})");
                break;
        }
    }

    // ── Argument parsing (defensive: any failure falls back to a plain render) ───────
    // Pull every value named `prop` (case-insensitive) from anywhere in the call's argument JSON.
    private static List<string> StringValues(FunctionCallContent call, string prop)
    {
        var found = new List<string>();
        if (call.Arguments is null) return found;
        try
        {
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(call.Arguments));
            Collect(doc.RootElement, prop, found);
        }
        catch { /* leave empty — caller falls back */ }
        return found;
    }

    private static void Collect(JsonElement el, string prop, List<string> into)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var p in el.EnumerateObject())
                {
                    if (string.Equals(p.Name, prop, StringComparison.OrdinalIgnoreCase))
                    {
                        var s = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
                        if (!string.IsNullOrWhiteSpace(s)) into.Add(s!);
                    }
                    Collect(p.Value, prop, into);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray()) Collect(item, prop, into);
                break;
        }
    }

    private static string? FirstString(FunctionCallContent call)
    {
        if (call.Arguments is null) return null;
        foreach (var kv in call.Arguments)
            if (kv.Value is string s && !string.IsNullOrWhiteSpace(s)) return s;
        return null;
    }

    private static string Args(FunctionCallContent call)
    {
        if (call.Arguments is not { Count: > 0 }) return "";
        var s = string.Join(", ", call.Arguments.Select(kv => $"{kv.Key}={kv.Value}"));
        return s.Length <= 60 ? s : s[..60] + "…";
    }

    // ── Human gate: YES is the default, so the whole demo flows on Enter ─────────────
    private static bool ApproveByDefault(string question)
    {
        Ui.Write($"\n  🔐 {question} ", Ui.Warn);
        Ui.Write("(Y/n) › ", ConsoleColor.White);
        var ans = (Console.ReadLine() ?? "").Trim();
        return !ans.StartsWith("n", StringComparison.OrdinalIgnoreCase);
    }
}
