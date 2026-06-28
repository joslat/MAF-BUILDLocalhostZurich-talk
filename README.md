# Microsoft Agent Framework — Demo Samples (.NET & Python)

Two beautiful, runnable console apps — one **.NET**, one **Python** — that bring the five
on-stage Microsoft Agent Framework samples to life.

> Every sample was written against the **real, current MAF API** (.NET 1.10.0 · Python 1.9.0)
> and **verified live** against a chat model — not an earlier draft, which had several
> fictional or renamed APIs (see each project's README for the diff).

## The five samples

| # | Sample | Shows | .NET | Python |
|---|--------|-------|:----:|:------:|
| 1 | **Handoff you can watch** | Triage routes to Orders/Returns; Returns escalates one-way to a terminal Fraud agent — every hop printed live | ✅ | ✅ |
| 2 | **Evals: green vs. red** | Same query, two builds — does each actually call the refund tool? | ✅ | ✅ |
| 3 | **CodeAct head-to-head** | ~20 tool calls vs. **one** sandboxed program — compare time & tokens | —* | ✅ |
| 4 | **Harness approval gate** | A risky shell tool that needs a human "yes" | ✅ | ✅ |
| 5 | **ShopBot — the finale** | Cooperate (handoff) + Act (approval) + Tested (eval), one app | ✅ | ✅ |

`*` CodeAct is Python-only today (.NET "coming soon").

## The one shared requirement: a chat model

No database, no server — just a chat model. Set **Azure OpenAI** (resolved first; matches the
reference `AIConfig.cs`) or **OpenAI** env vars, then run. Swap the client, keep the agent.

## Quick start

**.NET** (needs the .NET 9 SDK):
```bash
cd dotnet/MafDemos
dotnet run
```

**Python** (needs Python 3.10+):
```bash
cd python
python -m venv .venv && .venv\Scripts\activate      # or: source .venv/bin/activate
pip install -r requirements.txt
python main.py
```

See [`dotnet/README.md`](./dotnet/README.md) and [`python/README.md`](./python/README.md) for
setup details, the file map, and the "what the draft guide got wrong" tables.

## Repository layout

```
.
├─ dotnet/MafDemos/        .NET 9 console app (MAF 1.10.0)
├─ python/                 Python console app (agent-framework 1.9.0)
└─ README.md              you are here
```
