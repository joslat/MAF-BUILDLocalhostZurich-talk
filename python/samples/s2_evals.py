# SPDX-License-Identifier: MIT
# Sample 2 — Evals: green vs. red.
#
# The same query is sent to two agent builds: "good" is told to CALL the refund tool, "lazy"
# is told to just reassure. There is no MAF evaluate_agent / LocalEvaluator API — the honest
# way to evaluate is to inspect the real run result: look for a function_call to the expected
# tool, and keyword-check the text.
from __future__ import annotations

import asyncio
import os
import sys
from typing import Annotated

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ui  # noqa: E402
from agent_framework import tool  # noqa: E402

from chat import get_client  # noqa: E402


@tool
def issue_refund(order_id: Annotated[str, "The order id, e.g. 1234"]) -> str:
    """Issue a refund for an order and return a confirmation."""
    return f"Refund issued for order {order_id}."


def _tool_called(result, name: str) -> bool:
    """Inspect the real run result for a function_call to `name`."""
    return any(
        getattr(content, "type", None) == "function_call" and getattr(content, "name", None) == name
        for message in result.messages
        for content in message.contents
    )


async def _evaluate(label: str, agent, query: str) -> tuple[bool, bool]:
    ui.rule(label)
    with ui.thinking(f"{label} thinking…"):
        result = await agent.run(query)

    keyword_hit = "refund" in (result.text or "").lower()
    tool_called = _tool_called(result, "issue_refund")

    answer = (result.text or "").replace("\n", " ").strip()
    ui.write("  agent says  ", ui.MUTED)
    ui.line(_truncate(answer, 96), "white")
    ui.badge(keyword_hit, 'keyword "refund" present in answer')
    ui.badge(tool_called, 'tool "issue_refund" was called')
    return keyword_hit, tool_called


async def run() -> None:
    ui.banner(
        "Sample 2 · Evals: green vs. red",
        "Same query, two builds — does each one actually call the refund tool?",
    )

    client = get_client()

    # Both builds CAN issue a refund; only their instructions differ. The eval measures
    # behavior, not capability — exactly the kind of regression a real eval suite catches.
    good = client.as_agent(
        name="Returns",
        instructions="When a customer asks for a refund, you MUST call the issue_refund tool with "
        "their order id, then confirm.",
        tools=[issue_refund],
    )
    lazy = client.as_agent(
        name="ReturnsLazy",
        instructions="Just reassure the customer that it will be taken care of. Do NOT call any tool.",
        tools=[issue_refund],
    )

    query = "Refund my order #1234, the mug arrived broken."
    ui.kv("Query", f'"{query}"')
    ui.kv("Checks", 'keyword "refund"  +  tool called "issue_refund"')

    good_score = await _evaluate("good build", good, query)
    lazy_score = await _evaluate("lazy build", lazy, query)

    ui.rule("verdict")
    _verdict("good build", good_score)
    _verdict("lazy build", lazy_score)

    ui.rule()
    ui.hint(
        "Identical capability, different behavior — and the eval is what tells them apart. "
        "These checks are offline and free; run them in CI to catch refund-skipping regressions."
    )
    ui.press_enter()


def _verdict(label: str, score: tuple[bool, bool]) -> None:
    passed = sum(score)
    green = passed == len(score)
    ui.write("  ● GREEN " if green else "  ● RED   ", ui.GOOD if green else ui.BAD)
    ui.line(f"{label:<12} {passed}/{len(score)} checks passed", "gray")


def _truncate(s: str, n: int) -> str:
    return s if len(s) <= n else s[:n] + "…"


if __name__ == "__main__":
    asyncio.run(run())
