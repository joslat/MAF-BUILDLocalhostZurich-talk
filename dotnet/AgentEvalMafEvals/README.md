# AgentEval × MAF — agent evals & benchmarks

A menu-driven .NET console app that scores a real **Microsoft Agent Framework** agent with
**[AgentEval](https://www.nuget.org/packages/AgentEval)** and renders self-contained **HTML reports**.
Three categories:

- **Flat metrics** — a custom metric bundle via MAF's native `IAgentEvaluator`.
- **Agentic benchmark** — pick a preset composite (also via `IAgentEvaluator`).
- **Red-team** — OWASP / MITRE / NIST at **smoke / standard / audit-grade**, via AgentEval's `ScanAsync`.

Built against **AgentEval 0.13.2-beta** + **Microsoft Agent Framework 1.11.1** on **.NET 9** — all from
NuGet (`dotnet add package AgentEval --prerelease`).

---

## 1. Prerequisites

A chat model, via environment variables (Azure OpenAI first, then OpenAI):

```powershell
setx AZURE_OPENAI_ENDPOINT   "https://<your-resource>.openai.azure.com/"
setx AZURE_OPENAI_API_KEY    "<your-key>"
setx AZURE_OPENAI_DEPLOYMENT "gpt-4o-mini"
# …or:  setx OPENAI_API_KEY "sk-..."   (OPENAI_CHAT_MODEL optional, default gpt-4.1-mini)
```

The same client backs the agent under test **and** the LLM-as-judge AgentEval's metrics + scans use.

## 2. Run

```bash
cd dotnet/AgentEvalMafEvals
dotnet run                                  # interactive MENU — pick, run, view HTML, back to menu
dotnet run -- --benchmark agentic-execution # one-shot: flat metrics + that Agentic benchmark, then exit
dotnet run -- --no-open                     # CI/scripted: default benchmark, write but don't launch
```

The menu loops until you quit:

```
[1] Flat metrics            native IAgentEvaluator — custom metric bundle
[2] Agentic benchmark…      → submenu: pick a preset
[3] Red-team benchmark…     → submenu: family (OWASP / MITRE / NIST) → tier (smoke / standard / audit)
[q] Quit
```

Reports land in `dotnet/AgentEvalMafEvals/output/agenteval-*.html`. Every run is **fault-tolerant** — if
a judge rubric trips the provider content filter, that run is skipped with a message and you return to
the menu.

### Two evaluation styles

| Category | Path | Agent |
|---|---|---|
| Flat metrics · Agentic benchmark | MAF **native `IAgentEvaluator`** — `agent.EvaluateAsync(queries, evaluator)`. The full `EvalItem.Conversation` is forwarded, so code-based tool metrics see the real tool calls. | MAF `AIAgent` (with tools) |
| Red-team | AgentEval **`ScanAsync`** — sends adversarial probes and judges each response. (A scan can't be expressed as a fixed-query `IEvaluator`, so this path is intentionally *not* `IAgentEvaluator`.) | AgentEval `IStreamableAgent` (`AsEvaluableAgent`) |

### Agentic tiers + presets (submenu, or `--benchmark <key>`)

The first three are the **smoke / standard / audit-grade** tiers — mapped the way AgentEval's own
sample does it (Smoke → Tool-Call Accuracy, Standard & Audit-Grade → Agentic Execution). The rest are
specific presets.

| key | what | sub-evals |
|---|---|---|
| `smoke` *(default)* | **Smoke** — Tool-Call Accuracy | selection, input, output, success, efficiency |
| `standard` | **Standard** — Agentic Execution | task completion/adherence, intent, tool accuracy, navigation |
| `audit-grade` | **Audit-Grade** — Agentic Execution | same suite as Standard; pair with a stronger judge in production |
| `reasoning` | Reasoning | reasoning quality |
| `user-experience` | User Experience | tone, verbosity, refusal, calibration |
| `adversarial-direct` | Adversarial (direct) | injection / persona / jailbreak resistance |
| `rag-quality` | RAG Quality | faithfulness, relevance, context (best with retrieval context) |
| `full` | **Full suite** — all single-response categories | Execution + Reasoning + UX + Adversarial + RAG + **Safety** (~35 sub-evals; slow, costs more) |

(`tool-call-accuracy` and `agentic-execution` still work as `--benchmark` aliases for `smoke` / `standard`.)

### "Run the whole Agentic benchmark"

AgentEval's Agentic benchmark is **~60 evaluators in ~12 categories** ([how-it-works](https://agenteval.dev/benchmarks/agentic/how-it-works.html)), and by design **you pick a preset — there's no single "run all" preset**, because categories need different inputs.

**Full suite** (`--benchmark full`) runs every category that's meaningful **from one agent answer** — the
7 single-response categories: System & Process, RAG, Reasoning, UX, Calibration, Adversarial, **Safety**.
It runs **each category separately** (one shared response), so a content-filter or error in one category
is **attributed and skipped**, not fatal — the rest still complete and you get a combined report.

It deliberately **omits** the 5 categories that a one-shot prompt structurally can't produce:

| Omitted category | Needs |
|---|---|
| Operational / Telemetry, Efficiency | captured runtime latency / tokens / cost |
| Memory, Multi-turn | a multi-turn conversation |
| Judge Quality | a calibration corpus of prior judge results |

> **The true 60-evaluator / all-category run** is the dataset-driven CLI path —
> `agenteval bench agentic --preset <…>` over multi-turn scenarios with telemetry capture and a policy
> config — not a single-query demo. Note also (per the doc's calibration table) only **System & Process**
> and **RAG** are calibrated to HIGH today; the other categories run and produce real verdicts but are
> MEDIUM ("coverage gap").

### When the content filter hits

Some judge rubrics (especially Adversarial / Safety, whose prompts contain attack language) can trip
Azure OpenAI's content filter — an HTTP 400 on the **judge** call, not your agent. What this sample does:

- **Attributes it.** Running per-category (Full suite) or per-preset means you see *which* category
  tripped it — e.g. `Skipped: Safety (content filter)` — and the run continues.
- **Survives it.** A skipped category/preset returns you to the menu; the others still produce results.

What it can't do is *prevent* the filter (it's server-side on the prompt). To avoid it: point the judge
at an Azure deployment whose **content filter is relaxed / annotate-only** (Azure AI Foundry → your
deployment → content-filter config). The deeper fix is library-side — AgentEval's `CompositeEval` could
emit a *skipped leaf* (naming the exact filtered sub-eval) instead of aborting the composite.

### Red-team families × tiers (submenu)

| family | smoke | standard | audit-grade |
|---|---|---|---|
| **OWASP LLM Top 10** | a few probes | full Top-10 | comprehensive |
| **MITRE ATLAS** | a few techniques | ATLAS baseline | comprehensive |
| **NIST AI RMF** | quick | RMF baseline | comprehensive |

Audit-grade is thorough — slower and costs more (many probes × agent + judge calls).

## 3. Inside (clean responsibilities)

```
dotnet/AgentEvalMafEvals/
├─ Program.cs                       thin entry — setup + dispatch to EvalApp
├─ EvalApp.cs                       wires agents/runners/reporter; drives the category menu
├─ Tools/      TravelTools          the domain tools (SearchFlights, SearchHotels)
├─ Agents/     TravelAgentFactory   builds the MAF AIAgent and the AgentEval IStreamableAgent
├─ Infrastructure/
│  ├─ AiBackend.cs                  resolves the IChatClient + judge ChatConfiguration from env
│  ├─ Output.cs                     console toolkit (banner, key/value, tree printer)
│  └─ Menu.cs                       reusable single-choice menu
├─ Evaluation/
│  ├─ MafEvalRunner.cs              the native-IAgentEvaluator runs (flat + Agentic composite)
│  ├─ BenchmarkCatalog.cs          the Agentic presets, BenchmarkSelector.cs the --benchmark arg
│  ├─ RedTeamCatalog.cs            the red-team families × tiers (OWASP/MITRE/NIST)
│  └─ EvalRun.cs                    the shared result record (verdict + EvalResult tree)
└─ Reporting/  HtmlReportWriter     EvalResult tree → HtmlEvalResultRenderer → save + open
```

> **Heads-up:** the `agentic-execution` preset's judge rubrics sometimes trip Azure OpenAI's content
> filter (an intermittent HTTP 400 on the judge call); AgentEval's composite aborts the whole run when
> one sub-eval is blocked. Re-run it, pick another, or point the judge at a deployment with a relaxed
> content filter.

Background: AgentEval's [`using-agenteval-with-maf-evals.md`](https://github.com/AgentEvalHQ/AgentEval/blob/main/docs/using-agenteval-with-maf-evals.md).
