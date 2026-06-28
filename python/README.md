# MAF Demos — Python

A single, beautiful console app that runs the on-stage **Microsoft Agent Framework** samples:

| # | Sample | Shows |
|---|--------|-------|
| 1 | **Handoff you can watch** | Triage routes to Orders/Returns; Returns escalates to a terminal Fraud agent — every hop printed live |
| 2 | **Evals: green vs. red** | The same query against two agent builds — one calls the refund tool (green), one doesn't (red) |
| 3 | **CodeAct head-to-head** | The same tool-heavy task as ~20 tool calls vs. **one** sandboxed Hyperlight program — compare time & tokens |
| 4 | **Harness approval gate** | A risky shell tool that needs a human "yes" before it runs |
| 5 | **ShopBot — the finale** | One app that cooperates (handoff), acts (approval-gated refund), and is tested (eval) |

Built and verified against **agent-framework 1.9.0** on **Python 3.10+**.

---

## 1. Setup

```bash
cd python
python -m venv .venv
# Windows:        .venv\Scripts\activate
# macOS / Linux:  source .venv/bin/activate
pip install -r requirements.txt
```

Then configure a model backend — copy `.env.example` to `.env` and fill in **one** block:

- **Azure OpenAI** (resolved first; matches the reference `AIConfig.cs`):
  `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_API_KEY`, `AZURE_OPENAI_DEPLOYMENT`
- **or OpenAI:** `OPENAI_API_KEY` (+ optional `OPENAI_CHAT_MODEL`)

`chat.py` resolves the backend automatically — **swap the client, keep the agent.**

## 2. Run

```bash
python main.py                 # the menu
# or run one sample directly:
python samples/s1_handoff.py
python samples/s3_codeact.py
```

---

## 3. What's inside

```
python/
├─ main.py                  the menu
├─ chat.py                  builds the chat client from env vars (Azure OpenAI / OpenAI)
├─ ui.py                    console toolkit: banners, per-agent colors, badges, UTF-8/ANSI setup
└─ samples/
   ├─ s1_handoff.py         HandoffBuilder + the run/request-response loop
   ├─ s2_evals.py          two builds + honest function_call / keyword checks
   ├─ s3_codeact.py        HyperlightCodeActProvider vs. traditional tool-calling
   ├─ s4_harness.py        @tool(approval_mode="always_require") + approval loop
   └─ s5_shopbot.py        all three pillars on one cast
```

## 4. The real APIs (and what the draft guide got wrong)

Written against the **actual 1.9.0 API**, not an earlier draft guide:

| Area | Draft guide | Real 1.9.0 |
|---|---|---|
| Agent class | `Agent(...)` ✓ | `Agent(...)` / `client.as_agent(...)` ✓ |
| Tool decorator | `@tool` ✓ | `@tool` / `@tool(approval_mode="always_require")` ✓ |
| Handoff import | `from agent_framework_orchestrations import HandoffBuilder` | `from agent_framework.orchestrations import HandoffBuilder` |
| Handoff build | `.with_start_agent(a).add_handoff(a, [b])` ✓ | same — plus agents need `require_per_service_call_history_persistence=True` |
| Handoff run | `workflow.run_stream(...)` | `workflow.run(msg, stream=True)` → events; reply via `workflow.run(responses={id: HandoffAgentUserRequest.create_response(text)})` |
| Approval | `result.user_input_requests`, `req.to_function_approval_response(ok)` ✓ | same; resend `[query, Message("assistant",[req]), Message("user",[resp])]` |
| Evaluation | `evaluate_agent` / `LocalEvaluator` / `keyword_check` | **fictional** — inspect `result.messages` for `content.type == "function_call"` yourself |
| CodeAct | `from agent_framework_hyperlight import HyperlightCodeActProvider` | `from agent_framework.hyperlight import HyperlightCodeActProvider`; pass via `context_providers=[...]` |
| Azure client | `AzureOpenAIChatClient(...)` | not in the meta package — `OpenAIChatCompletionClient(deployment, api_key=, azure_endpoint=, api_version=)` works against Azure |

## 5. Safety notes

- **Sample 4's shell tool is simulated** — it never executes a real command. In production, run it inside an isolated environment (container / VM) and keep approvals on.
- **Sample 3 (CodeAct)** runs code in a Hyperlight micro-VM. If the runtime isn't available, the sample explains CodeAct and runs the traditional side only.
