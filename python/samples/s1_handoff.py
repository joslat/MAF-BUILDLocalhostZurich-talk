# SPDX-License-Identifier: MIT
# Sample 1 — Handoff you can watch.
#
# A Triage agent routes the customer to Orders or Returns; Returns can escalate to a terminal
# Fraud agent. Every hop is printed live via the workflow's `handoff_sent` events. Built with
# the real MAF handoff orchestration: HandoffBuilder(...).with_start_agent(...).add_handoff(...).
from __future__ import annotations

import asyncio
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ui  # noqa: E402  (imported first: sets UTF-8 + silences import warnings)
from agent_framework import AgentResponse  # noqa: E402
from agent_framework.orchestrations import HandoffAgentUserRequest, HandoffBuilder  # noqa: E402

from chat import get_client  # noqa: E402


async def run() -> None:
    ui.banner(
        "Sample 1 · Handoff you can watch",
        "Triage → Orders / Returns · Returns ⇢ Fraud (escalation)",
    )

    client = get_client()

    # Handoff workflows require this flag so each agent's local history stays consistent
    # with the service across handoff tool-call short-circuits.
    persist = {"require_per_service_call_history_persistence": True}

    triage = client.as_agent(
        name="Triage",
        instructions="You are the front desk. Route the customer to the right specialist and hand "
        "off immediately. Do not answer order or return questions yourself.",
        description="Routes the customer to Orders or Returns",
        **persist,
    )
    orders = client.as_agent(
        name="Orders",
        instructions="You answer order-status questions concisely and politely.",
        description="Handles order-status questions",
        **persist,
    )
    returns = client.as_agent(
        name="Returns",
        instructions="You handle returns and refunds. If a request looks like abuse or fraud "
        "(many recent refunds, contradictory claims), hand off to Fraud.",
        description="Handles returns and refunds",
        **persist,
    )
    fraud = client.as_agent(
        name="Fraud",
        instructions="You handle suspected fraud. State that the case is escalated for review. "
        "This is the end of the line.",
        description="Handles suspected fraud or abuse — terminal",
        **persist,
    )

    # The graph: edges declare what's allowed. No edge = no handoff.
    workflow = (
        HandoffBuilder(participants=[triage, orders, returns, fraud])
        .with_start_agent(triage)
        .add_handoff(triage, [orders, returns])  # triage → specialists
        .add_handoff(orders, [triage])  # let specialists hand back
        .add_handoff(returns, [fraud, triage])  # one-way escalation + hand back
        .build()
    )

    id_to_name = {a.id: a.name for a in (triage, orders, returns, fraud)}

    ui.rule("conversation")
    ui.dim('Try:  "Where is my order #1234?"   ·   "My mug arrived smashed, refund order #1234"')
    ui.dim('      "This is my 9th refund this week, give me my money"   ·   type \'exit\' to finish')

    first = ui.prompt("customer")
    if first is None or first.strip().lower() == "exit":
        return

    events = workflow.run(first, stream=True)
    pending = await _handle_events([e async for e in events], id_to_name)

    while pending:
        nxt = ui.prompt("customer")
        if nxt is None or nxt.strip().lower() == "exit":
            await workflow.run(responses={r.request_id: HandoffAgentUserRequest.terminate() for r in pending})
            break
        responses = {r.request_id: HandoffAgentUserRequest.create_response(nxt) for r in pending}
        pending = await _handle_events(await workflow.run(responses=responses), id_to_name)

    ui.rule()
    ui.hint(
        "Handoffs are just tool calls: the model invokes a handoff function the builder injected. "
        "No edge = no route — that's why Fraud can't hand back."
    )
    ui.press_enter()


def _name(value: str, id_to_name: dict[str, str]) -> str:
    return id_to_name.get(value, value)


async def _handle_events(events, id_to_name: dict[str, str]) -> list:
    """Print handoffs + agent speech, and collect any pending user-input requests."""
    pending: list = []
    for event in events:
        if event.type == "handoff_sent":
            src, tgt = _name(event.data.source, id_to_name), _name(event.data.target, id_to_name)
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
