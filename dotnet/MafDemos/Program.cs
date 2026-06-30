// SPDX-License-Identifier: MIT
// Microsoft Agent Framework — Build & Run the Demo Samples (.NET)
//
// A single console app with a menu that runs the on-stage samples:
//   1 Handoff · 2 Evals · 3 CodeAct (Python-only) · 4 Agent harness · 5 ShopBot
//
// The one shared requirement is a chat model (see Infrastructure/Chat.cs).

using MafDemos.Infrastructure;
using MafDemos.Samples;
using Microsoft.Extensions.AI;

Ui.Init();

if (!Chat.IsConfigured)
{
    Ui.Banner("Microsoft Agent Framework · Demo Samples",
        "No model backend configured");
    Ui.Failure("Set a model backend before running the samples:");
    Ui.Info("• Azure OpenAI : AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY + AZURE_OPENAI_DEPLOYMENT");
    Ui.Info("• OpenAI       : OPENAI_API_KEY  (+ optional OPENAI_CHAT_MODEL)");
    Ui.Dim("\nThen run again with:  dotnet run");
    return;
}

using IChatClient chat = Chat.CreateClient();

var samples = new (string Key, string Title, string Blurb, Func<IChatClient, Task>? Run)[]
{
    ("1", "Handoff you can watch", "Triage routes; Returns escalates one-way to Fraud", S1_Handoff.RunAsync),
    ("2", "Evals: green vs. red", "Same query, two builds — does each call the tool?", S2_Evals.RunAsync),
    ("3", "CodeAct head-to-head", "Python-only today (.NET \"coming soon\")", null),
    ("4", "A real agent harness", "Plans with todos, executes, asks before it books", S4_Harness.RunAsync),
    ("5", "ShopBot — the finale", "Cooperate + Act + Tested, in one small app", S5_ShopBot.RunAsync),
};

while (true)
{
    TryClear();
    Ui.Banner("Microsoft Agent Framework · Demo Samples", $"Backend  ·  {Chat.Backend}");

    foreach (var s in samples)
    {
        var enabled = s.Run is not null;
        Ui.Write($"   {s.Key}  ", enabled ? Ui.Accent : Ui.Muted);
        Ui.Write($"{s.Title,-26}", enabled ? ConsoleColor.White : Ui.Muted);
        Ui.Line(s.Blurb, Ui.Muted);
    }
    Ui.Write("   q  ", Ui.Accent);
    Ui.Line("Quit", ConsoleColor.White);

    var raw = Ui.Prompt("choose a sample");
    if (raw is null) break;                       // EOF (Ctrl-Z / piped) → quit
    var choice = raw.Trim().ToLowerInvariant();
    if (choice is "q" or "quit" or "exit" or "0") break;

    var picked = Array.Find(samples, s => s.Key == choice);
    if (picked.Key is null)
    {
        Ui.Dim("  (unknown choice)");
        await Task.Delay(600);
        continue;
    }

    TryClear();
    try
    {
        if (picked.Run is not null)
        {
            await picked.Run(chat);
        }
        else
        {
            ShowCodeActNotice();
        }
    }
    catch (Exception ex)
    {
        Ui.Rule();
        Ui.Failure($"Sample failed: {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException is { } inner)
            Ui.Dim($"  ↳ {inner.GetType().Name}: {inner.Message}");
        Ui.PressAnyKey();
    }
}

Ui.Line("\n  Thanks for exploring the Microsoft Agent Framework. 👋\n", Ui.Accent);

static void ShowCodeActNotice()
{
    Ui.Banner("Sample 3 · CodeAct head-to-head",
        "Run the same tool-heavy task two ways and compare time & tokens");
    Ui.Info("CodeAct compares normal tool-calling against running ONE sandboxed program that");
    Ui.Info("calls the tools itself — a big token saving on tool-heavy tasks.");
    Ui.Line("", ConsoleColor.Gray);
    Ui.Hint("CodeAct is Python-only today (.NET is \"coming soon\"). See the Python demo:");
    Ui.Info("• python/samples/s3_codeact.py");
    Ui.PressAnyKey();
}

static void TryClear()
{
    try { Console.Clear(); } catch { /* redirected / headless */ }
}
