# MAF Demos — .NET

A single, beautiful console app that runs the on-stage **Microsoft Agent Framework** samples:

| # | Sample | Shows | .NET |
|---|--------|-------|:----:|
| 1 | **Handoff you can watch** | Triage routes to Orders/Returns; Returns escalates one-way to a terminal Fraud agent — every hop printed live | ✅ |
| 2 | **Evals: green vs. red** | The same query against two agent builds — one calls the refund tool (green), one doesn't (red) | ✅ |
| 3 | **CodeAct head-to-head** | Tool-calling vs. one sandboxed program | Python-only* |
| 4 | **A real agent harness** | `AsHarnessAgent`: a HarnessAgent that plans with a todo list, executes tools, then asks before it books | ✅ |
| 5 | **ShopBot — the finale** | One app that cooperates (handoff), acts (approval-gated refund), and is tested (eval) | ✅ |

`*` CodeAct is Python-only today (.NET "coming soon"); menu item 3 explains it and points at the Python demo.

Built and verified against **Microsoft Agent Framework 1.10.0** on **.NET 9** (Sample 4 also pulls
`Microsoft.Agents.AI.Harness` **1.10.0-preview** for the real `AsHarnessAgent`).

> **Two apps live in this folder.** The `MafDemos` menu app above (MAF **1.10.0**), plus
> **`AgentEvalMafEvals`** — a focused sample that scores a MAF agent with
> [AgentEval](https://www.nuget.org/packages/AgentEval) through MAF's evaluation feature
> (`agent.EvaluateAsync`, in `Microsoft.Agents.AI` since **1.2.0**) and renders an HTML report. See **[§6](#6-agenteval-evals-agentevalmafevals--maf-1111)**.

---

## 1. Prerequisites

- **.NET 9 SDK** (or newer) — `dotnet --version`
- **A chat model.** That's the only hard dependency. Either:
  - **Azure OpenAI** (matches the reference `AIConfig.cs`):
    ```powershell
    setx AZURE_OPENAI_ENDPOINT   "https://<your-resource>.openai.azure.com/"
    setx AZURE_OPENAI_API_KEY    "<your-key>"
    setx AZURE_OPENAI_DEPLOYMENT "gpt-4o-mini"   # your deployed model name
    ```
  - **or OpenAI:**
    ```powershell
    setx OPENAI_API_KEY    "sk-..."
    setx OPENAI_CHAT_MODEL "gpt-4.1-mini"        # optional, this is the default
    ```

The app resolves the backend automatically (Azure OpenAI first, then OpenAI) — **swap the client, keep the agent.**

## 2. Run

```bash
cd dotnet/MafDemos
dotnet run
```

Pick a sample from the menu. Each sample is self-contained in `Samples/`, so you can read one file end-to-end.

---

## 3. What's inside

```
dotnet/MafDemos/
├─ Program.cs                  the menu
├─ Infrastructure/
│  ├─ Chat.cs                  builds the IChatClient from env vars (Azure OpenAI / OpenAI)
│  └─ Ui.cs                    console toolkit: banners, per-agent colors, spinner, badges
└─ Samples/
   ├─ S1_Handoff.cs            handoff workflow + live streaming run loop
   ├─ S2_Evals.cs             two builds + honest tool-call / keyword checks
   ├─ S4_Harness.cs           AsHarnessAgent: plan (todos) → execute → human gate → book
   └─ S5_ShopBot.cs           all three pillars on one cast
```

## 4. The real APIs (and what the draft guide got wrong)

These samples were written against the **actual 1.10.0 API**, not an earlier draft guide. Notable differences found while building:

| Area | Draft guide | Real 1.10.0 |
|---|---|---|
| Start a workflow run | `InProcessExecution.OpenStreamingAsync(workflow)` | `InProcessExecution.RunStreamingAsync(workflow, messages)` (seeds input) |
| Kick off a turn | (implicit) | send a `new TurnToken(emitEvents: true)` |
| Return-to-previous | `.EnableReturnToPrevious()` | doesn't exist — add **reverse handoffs** (`WithHandoffs([specialists], triage)`) |
| Streamed event | `AgentResponseUpdateEvent` ✓ | `AgentResponseUpdateEvent` — `.Update.Text`, `.Update.AuthorName`, `.ExecutorId` |
| Handoff function | `handoff_to_<agent>` | `handoff_to_<index>` (per-source 1-based) — so we name the hop from the *next* speaker |
| Evaluation | `LocalEvaluator` / `agent.EvaluateAsync(...)` / `ExpectedToolCall` | **These exist** — present in `Microsoft.Agents.AI` since **1.2.0** (Apr 2026, PR #4914). The earlier "fictional" note was wrong; S2 just keeps it offline/free with manual `FunctionCallContent` checks. *(For the real eval API, see the `AgentEvalMafEvals` project, [§6](#6-agenteval-evals-agentevalmafevals--maf-1111).)* |
| Approval content | `FunctionApprovalRequestContent` / `req.FunctionCall` | `ToolApprovalRequestContent` / `req.ToolCall` (cast to `FunctionCallContent`) / `req.CreateResponse(bool)` |
| Approval wrapper | `new ApprovalRequiredAIFunction(AIFunctionFactory.Create(...))` ✓ | same ✓ |
| Session | `agent.CreateSessionAsync()` ✓ | `agent.CreateSessionAsync()` → `AgentSession`; `agent.RunAsync(messages, session)` → `AgentResponse` ✓ |
| Agent harness | (not in the draft guide) | **`chatClient.AsHarnessAgent(HarnessAgentOptions)`** → `HarnessAgent` (package `Microsoft.Agents.AI.Harness`, preview): a `TodoProvider` (planning), `AgentModeProvider` (plan/execute), compaction, and tool approval are all pre-wired. Its full interactive UX lives in the sample-only reactive **`HarnessConsole`**, whose runner *injects* approval responses mid-run — so outside that runner, gate a risky step with a normal follow-up turn (Sample 4 does). |

> The framework is young and evolving; package names and a few signatures drift between releases. Everything here compiles and runs against 1.10.0.

## 5. Safety notes

- **Sample 4's `book_trip` tool is simulated** — it never books or charges anything. A real harness can run shell / file tools; in production keep tool approval on and run the executor inside an isolated environment (container / VM).
- The samples use an **API key** (`AzureKeyCredential`) for simplicity. In production, prefer a managed identity / specific `TokenCredential`.

---

## 6. AgentEval evals (`AgentEvalMafEvals`) — MAF 1.11.1

A second, focused console app: it scores a real MAF agent with
**[AgentEval](https://www.nuget.org/packages/AgentEval)** metrics using MAF's built-in evaluation
feature (`agent.EvaluateAsync` — in `Microsoft.Agents.AI` since **1.2.0**, Apr 2026), then renders the
result to a self-contained **HTML report**. Everything comes from NuGet — `AgentEval 0.13.2-beta`, which
depends on `Microsoft.Agents.AI 1.11.1` (the version this sample builds against).

**Flat metrics + Agentic benchmarks** run via MAF's native **`IAgentEvaluator`**
(`agent.EvaluateAsync(queries, evaluator)`) — that path forwards the *full* `EvalItem.Conversation`, so
even AgentEval's code-based tool metrics see the real tool calls. **Red-team** (OWASP / MITRE / NIST)
runs via AgentEval's **`ScanAsync`** pipeline (adversarial probes, judge-graded) — a scan can't be a
fixed-query `IEvaluator`, so that path uses an AgentEval `IStreamableAgent` instead.

> `MafDemos` stays on MAF 1.10.0; this project is separate and targets 1.11.1, so the two coexist in
> the solution without touching each other.

### Run

```bash
cd dotnet/AgentEvalMafEvals
dotnet run                                  # interactive MENU — pick a benchmark, run, repeat
dotnet run -- --benchmark agentic-execution # one-shot: flat metrics + that benchmark, then exit
dotnet run -- --no-open                     # CI/scripted: default benchmark, write but don't launch
```

Same model env vars as `MafDemos` (Azure OpenAI first, then OpenAI). Reports land in
`dotnet/AgentEvalMafEvals/output/agenteval-*.html`.

### Three categories (a looping menu)

```
[1] Flat metrics            native IAgentEvaluator — custom metric bundle
[2] Agentic benchmark…      → submenu: smoke / standard / audit-grade tiers, the reasoning ·
                                        user-experience · adversarial-direct · rag-quality presets,
                                        and a Full suite (all 7 single-response categories incl. Safety)
[3] Red-team benchmark…     → submenu: OWASP / MITRE / NIST  →  smoke / standard / audit-grade
[q] Quit
```

Each run is fault-tolerant — if a judge rubric trips the provider content filter, that run is skipped
with a message and you return to the menu.

### Inside

```
dotnet/AgentEvalMafEvals/
├─ Program.cs                     thin entry — setup + dispatch to EvalApp
├─ EvalApp.cs                     wires agents/runners/reporter; drives the category menu
├─ Tools/      TravelTools        the domain tools (SearchFlights, SearchHotels)
├─ Agents/     TravelAgentFactory builds the MAF AIAgent + the AgentEval IStreamableAgent
├─ Infrastructure/                AiBackend (client+judge) · Output (console) · Menu (reusable picker)
├─ Evaluation/                    MafEvalRunner · BenchmarkCatalog/Selector (Agentic) · RedTeamCatalog (OWASP/MITRE/NIST) · EvalRun
└─ Reporting/  HtmlReportWriter   EvalResult tree → HtmlEvalResultRenderer → save + open
```

Background: AgentEval's [`using-agenteval-with-maf-evals.md`](https://github.com/AgentEvalHQ/AgentEval/blob/main/docs/using-agenteval-with-maf-evals.md).
