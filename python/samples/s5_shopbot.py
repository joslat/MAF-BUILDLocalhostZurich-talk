# SPDX-License-Identifier: MIT
# Sample 5 — ShopBot (all three pillars), the "build this first" finale.
#
#   • cooperates  — a Triage→Orders/Returns→Fraud handoff graph (Sample 1)
#   • acts        — an approval-gated issue_refund tool (Sample 4)
#   • is tested   — a closing behavior eval on the Returns build (Sample 2)
#
# It's Samples 1 + 4 + 2 on a single shared cast, shown as three short acts.
from __future__ import annotations

import asyncio
import os
import sys
from typing import Annotated, Any

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ui  # noqa: E402
from agent_framework import AgentResponse, Message, tool  # noqa: E402
from agent_framework.orchestrations import HandoffAgentUserRequest, HandoffBuilder  # noqa: E402

from chat import get_client  # noqa: E402


@tool
def lookup_order(order_id: Annotated[str, "The order id, e.g. 1234"]) -> str:
    """Look up an order by id and return a short status line."""
    return f"Order {order_id}: 1x Blue Mug, $18.00 — delivered on Tuesday."


@tool(name="issue_refund", approval_mode="always_require")
def issue_refund_gated(order_id: Annotated[str, "The order id, e.g. 1234"]) -> str:
    """Issue a refund for an order (gated: requires human approval)."""
    return f"Refund of $18.00 issued for order {order_id}."


@tool(name="issue_refund")
def issue_refund_plain(order_id: Annotated[str, "The order id, e.g. 1234"]) -> str:
    """Issue a refund for an order and return a confirmation."""
    return f"Refund of $18.00 issued for order {order_id}."


def _tool_called(result, name: str) -> bool:
    return any(
        getattr(c, "type", None) == "function_call" and getattr(c, "name", None) == name
        for m in result.messages
        for c in m.contents
    )


async def run() -> None:
    ui.banner(
        "Sample 5 · ShopBot — the finale",
        "Cooperate (handoff) · Act (approval-gated refund) · Tested (eval)",
    )

    client = get_client()
    persist = {"require_per_service_call_history_persistence": True}

    # ── Act I — COOPERATE: a handoff graph routes the customer ──────────────────────────
    ui.rule("act I · cooperate — the handoff graph")

    triage = client.as_agent(
        name="Triage",
        instructions="You are the front desk. Route the customer to Orders or Returns and hand off. "
        "Don't answer yourself.",
        description="Routes the customer to Orders or Returns",
        **persist,
    )
    orders = client.as_agent(
        name="Orders",
        instructions="You answer order-status questions. Use lookup_order to get the facts, then reply briefly.",
        description="Handles order-status questions",
        tools=[lookup_order],
        **persist,
    )
    returns = client.as_agent(
        name="Returns",
        instructions="You handle returns and refunds. Use lookup_order to verify the order. Escalate abuse to Fraud.",
        description="Handles returns and refunds",
        tools=[lookup_order],
        **persist,
    )
    fraud = client.as_agent(
        name="Fraud",
        instructions="You handle suspected fraud. Say the case is escalated. This is the end of the line.",
        description="Suspected fraud — terminal",
        **persist,
    )

    workflow = (
        HandoffBuilder(participants=[triage, orders, returns, fraud])
        .with_start_agent(triage)
        .add_handoff(triage, [orders, returns])
        .add_handoff(orders, [triage])
        .add_handoff(returns, [fraud, triage])
        .build()
    )
    id_to_name = {a.id: a.name for a in (triage, orders, returns, fraud)}

    question = "Where is my order #1234?"
    ui.kv("Customer", f'"{question}"')
    pending = await _handle_turn([e async for e in workflow.run(question, stream=True)], id_to_name)
    if pending:  # close the conversation cleanly
        await workflow.run(responses={r.request_id: HandoffAgentUserRequest.terminate() for r in pending})

    # ── Act II — ACT: an approval-gated refund ──────────────────────────────────────────
    ui.rule("act II · act — the approval gate")

    returns_desk = client.as_agent(
        name="Returns",
        instructions="You process refunds. When asked for a refund, call issue_refund with the order id.",
        tools=[lookup_order, issue_refund_gated],
    )

    refund_ask = "My mug arrived smashed — please refund order #1234."
    ui.kv("Customer", f'"{refund_ask}"')

    with ui.thinking("Returns is working…"):
        result = await returns_desk.run(refund_ask)

    new_inputs: list[Any] = [refund_ask]  # seed once; accumulate across approval rounds
    while result.user_input_requests:
        for req in result.user_input_requests:
            print()
            ui.line(f"  ┌─ approval required · {req.function_call.name} ─────────────", ui.WARN)
            ui.line("  └ a refund moves real money — that's why it's gated", ui.WARN)
            ok = ui.confirm(f"approve {req.function_call.name}?")
            ui.success("approved") if ok else ui.failure("denied")
            new_inputs.append(Message("assistant", [req]))
            new_inputs.append(Message("user", [req.to_function_approval_response(ok)]))
        with ui.thinking("continuing…"):
            result = await returns_desk.run(new_inputs)

    ui.agent_line("Returns", (result.text or "").strip())

    # ── Act III — TESTED: closing behavior eval ─────────────────────────────────────────
    ui.rule("act III · tested — the closing eval")

    eval_returns = client.as_agent(
        name="Returns",
        instructions="When asked for a refund, you MUST call issue_refund with the order id.",
        tools=[issue_refund_plain],
    )
    with ui.thinking("running behavior eval…"):
        eval_result = await eval_returns.run("Refund my order #1234.")
    called_refund = _tool_called(eval_result, "issue_refund")
    ui.badge(called_refund, "Returns build issues a refund when asked  (issue_refund called)")

    ui.rule()
    ui.hint(
        "That's the whole story: agents that cooperate, act under human approval, and are "
        "tested for the behavior you care about — one small app, three pillars."
    )
    ui.press_enter()


async def _handle_turn(events, id_to_name: dict[str, str]) -> list:
    pending: list = []
    for event in events:
        if event.type == "handoff_sent":
            src = id_to_name.get(event.data.source, event.data.source)
            tgt = id_to_name.get(event.data.target, event.data.target)
            ui.write("\n  ⇢ ", ui.MUTED)
            ui.write(src, ui.color_for(src))
            ui.write("  hands off to  ", ui.MUTED)
            ui.line(tgt, ui.color_for(tgt))
        elif event.type == "output" and isinstance(event.data, AgentResponse):
            for message in event.data.messages:
                if message.text:
                    ui.agent_line(message.author_name or str(message.role), message.text)
        elif event.type == "request_info" and isinstance(event.data, HandoffAgentUserRequest):
            for message in event.data.agent_response.messages:
                if message.text:
                    ui.agent_line(message.author_name or str(message.role), message.text)
            pending.append(event)
    return pending


if __name__ == "__main__":
    asyncio.run(run())
