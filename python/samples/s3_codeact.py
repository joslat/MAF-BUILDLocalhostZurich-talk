# SPDX-License-Identifier: MIT
# Sample 3 — CodeAct head-to-head (Python only).
#
# The same tool-heavy task run two ways:
#   • Traditional — the model calls the tool once per item (many round-trips)
#   • CodeAct     — the model writes ONE program that calls the tools itself, run in a
#                   Hyperlight micro-VM sandbox (agent_framework.hyperlight)
# …then we compare wall-clock time (and tokens, when the result exposes them).
#
# CodeAct needs the Hyperlight runtime; if it isn't available on this machine the sample
# explains CodeAct and runs the Traditional side only.
from __future__ import annotations

import asyncio
import os
import sys
import time
from typing import Annotated

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ui  # noqa: E402
from agent_framework import tool  # noqa: E402

from chat import get_client  # noqa: E402

N = 20
INSTRUCTIONS = "Compute the requested per-user order totals using the tools, then report the sum."
PROMPT = f"Compute the order totals for users 1 through {N} and give me the grand total."


@tool
def get_order_total(user_id: Annotated[int, "The user id"]) -> float:
    """Return the order total for a single user (stand-in for a real per-user lookup)."""
    return user_id * 3.5


def _tokens(result) -> int | None:
    # usage_details may be a dict or a UsageDetails object depending on the client — handle both.
    usage = getattr(result, "usage_details", None)
    if usage is None:
        return None
    if isinstance(usage, dict):
        return usage.get("total_token_count") or usage.get("total_tokens")
    total = getattr(usage, "total_token_count", None)
    if total:
        return total
    inp = getattr(usage, "input_token_count", 0) or 0
    out = getattr(usage, "output_token_count", 0) or 0
    return (inp + out) or None


def _calls(result) -> int:
    return sum(
        1
        for m in result.messages
        for c in m.contents
        if getattr(c, "type", None) == "function_call"
    )


async def run() -> None:
    ui.banner(
        "Sample 3 · CodeAct head-to-head",
        "Tool-calling vs. one sandboxed program — compare time & tokens",
    )

    client = get_client()
    ui.kv("Task", f'"{PROMPT}"')
    ui.rule("traditional — one tool call per item")

    trad_agent = client.as_agent(name="Traditional", instructions=INSTRUCTIONS, tools=[get_order_total])
    with ui.thinking("Traditional agent working…"):
        t0 = time.perf_counter()
        trad = await trad_agent.run(PROMPT)
        trad_dt = time.perf_counter() - t0
    _report("Traditional", trad, trad_dt)

    ui.rule("codeact — one sandboxed program")
    try:
        from agent_framework.hyperlight import HyperlightCodeActProvider

        codeact = HyperlightCodeActProvider(tools=[get_order_total], approval_mode="never_require")
        ca_agent = client.as_agent(name="CodeAct", instructions=INSTRUCTIONS, context_providers=[codeact])
        with ui.thinking("CodeAct agent working (spinning up a micro-VM)…"):
            t0 = time.perf_counter()
            ca = await ca_agent.run(PROMPT)
            ca_dt = time.perf_counter() - t0
        _report("CodeAct", ca, ca_dt)
        _compare(trad, trad_dt, ca, ca_dt)
    except Exception as ex:  # noqa: BLE001 — graceful: explain instead of crashing
        ui.failure(f"CodeAct sandbox unavailable here: {type(ex).__name__}: {str(ex)[:120]}")
        ui.dim("CodeAct needs the Hyperlight runtime (Linux/Windows). Install with:")
        ui.dim("  pip install agent-framework-hyperlight --pre")
        ui.info("")
        ui.info(f"What it would show: instead of ~{N} separate tool calls, the model writes ONE")
        ui.info("program that loops over the tools inside a sandbox — typically a big token saving")
        ui.info("on tool-heavy tasks (the official benchmark reports ~64%).")

    ui.rule()
    ui.hint(
        "CodeAct trades many model round-trips for one sandboxed program — the more tool calls "
        "a task needs, the bigger the win."
    )
    ui.press_enter()


def _report(label: str, result, dt: float) -> None:
    ui.write(f"  {label:<12} ", ui.color_for(label))
    parts = [f"{dt:5.2f}s", f"{_calls(result)} tool calls"]
    toks = _tokens(result)
    if toks:
        parts.append(f"{toks} tokens")
    ui.line(" · ".join(parts), "white")
    ui.dim("  ↳ " + (result.text or "").replace("\n", " ").strip()[:90])


def _compare(trad, trad_dt: float, ca, ca_dt: float) -> None:
    tt, ct = _tokens(trad), _tokens(ca)
    if tt and ct and tt > 0:
        saving = 100 * (tt - ct) / tt
        ui.rule("verdict")
        if saving >= 0:
            ui.success(f"CodeAct used {saving:.0f}% fewer tokens than Traditional ({ct} vs {tt})")
        else:
            ui.dim(f"CodeAct used {-saving:.0f}% more tokens here ({ct} vs {tt}) — the win grows with more tool calls")


if __name__ == "__main__":
    asyncio.run(run())
