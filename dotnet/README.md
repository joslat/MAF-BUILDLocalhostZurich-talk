# MAF Demos — .NET

A single, beautiful console app that runs the on-stage **Microsoft Agent Framework** samples:

| # | Sample | Shows | .NET |
|---|--------|-------|:----:|
| 1 | **Handoff you can watch** | Triage routes to Orders/Returns; Returns escalates one-way to a terminal Fraud agent — every hop printed live | ✅ |
| 2 | **Evals: green vs. red** | The same query against two agent builds — one calls the refund tool (green), one doesn't (red) | ✅ |
| 3 | **CodeAct head-to-head** | Tool-calling vs. one sandboxed program | Python-only* |
| 4 | **Harness approval gate** | A risky shell tool that needs a human "yes" before it runs | ✅ |
| 5 | **ShopBot — the finale** | One app that cooperates (handoff), acts (approval-gated refund), and is tested (eval) | ✅ |

`*` CodeAct is Python-only today (.NET "coming soon"); menu item 3 explains it and points at the Python demo.

Built and verified against **Microsoft Agent Framework 1.10.0** on **.NET 9**.

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
   ├─ S4_Harness.cs           ApprovalRequiredAIFunction + approval loop
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
| Evaluation | `LocalEvaluator` / `agent.EvaluateAsync(...)` / `ExpectedToolCall` | **fictional** — inspect `response.Messages → FunctionCallContent` yourself |
| Approval content | `FunctionApprovalRequestContent` / `req.FunctionCall` | `ToolApprovalRequestContent` / `req.ToolCall` (cast to `FunctionCallContent`) / `req.CreateResponse(bool)` |
| Approval wrapper | `new ApprovalRequiredAIFunction(AIFunctionFactory.Create(...))` ✓ | same ✓ |
| Session | `agent.CreateSessionAsync()` ✓ | `agent.CreateSessionAsync()` → `AgentSession`; `agent.RunAsync(messages, session)` → `AgentResponse` ✓ |

> The framework is young and evolving; package names and a few signatures drift between releases. Everything here compiles and runs against 1.10.0.

## 5. Safety notes

- **Sample 4's shell tool is simulated** — it never executes a real command. In production, run it inside an isolated environment (container / VM) and keep approvals on.
- The samples use an **API key** (`AzureKeyCredential`) for simplicity. In production, prefer a managed identity / specific `TokenCredential`.
