# SPDX-License-Identifier: MIT
# Sample 4 — The harness approval gate.
#
# A risky shell tool that requires a human "yes" before it runs. The tool is decorated with
# @tool(approval_mode="always_require"), so the agent pauses and surfaces an approval request
# instead of executing. You approve (or deny), and the run continues with the context resent.
#
# SAFETY: the shell tool here is SIMULATED — it never executes real commands. In production,
# run it inside an isolated environment (container / VM / sandbox) and keep approvals on.
from __future__ import annotations

import asyncio
import os
import sys
from typing import Annotated, Any

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

import ui  # noqa: E402
from agent_framework import Message, tool  # noqa: E402

from chat import get_client  # noqa: E402


@tool(approval_mode="always_require")  # the gate
def run_bash(command: Annotated[str, "The bash command to run"]) -> str:
    """Run a bash command on the host and return its stdout. Potentially destructive."""
    # SIMULATED for the demo — reports success without executing. Replace with real
    # execution inside a sandbox (container / VM) in production.
    return f"[sandbox · simulated] command completed, exit code 0:\n$ {command}"


async def run() -> None:
    ui.banner(
        "Sample 4 · The harness approval gate",
        'A risky shell tool that needs a human "yes" before it runs',
    )

    client = get_client()
    ops = client.as_agent(
        name="Ops",
        instructions="You are an operations assistant. Use the run_bash tool to accomplish tasks. "
        "Prefer a single, specific command. Avoid destructive commands.",
        tools=[run_bash],
    )

    task = ui.prompt("ask Ops to do something (or Enter for the default)")
    if not task or not task.strip():
        task = "Clean up old *.log files in the system temp folder."
    ui.kv("Task", f'"{task}"')

    with ui.thinking("Ops is deciding what to run…"):
        result = await ops.run(task)

    if not result.user_input_requests:
        ui.dim("(The agent answered without requesting any gated tool.)")

    # No thread → resend the full context each round. Seed once and accumulate so that a
    # second approval round still carries the earlier request/response pairs.
    new_inputs: list[Any] = [task]
    while result.user_input_requests:
        for req in result.user_input_requests:
            _print_gate(req)
            approved = ui.confirm(f"approve {req.function_call.name}?")
            if approved:
                ui.success("approved — the tool will run")
            else:
                ui.failure("denied — the tool will be skipped")
            new_inputs.append(Message("assistant", [req]))
            new_inputs.append(Message("user", [req.to_function_approval_response(approved)]))

        with ui.thinking("continuing the run…"):
            result = await ops.run(new_inputs)

    ui.rule("result")
    ui.agent_line("Ops", (result.text or "").strip())

    ui.rule()
    ui.hint(
        "The gate is the decorator: approval_mode='always_require' makes the agent emit an "
        "approval request and wait. Drop it and the command would run unattended."
    )
    ui.press_enter()


def _print_gate(req) -> None:
    args = str(getattr(req.function_call, "arguments", "") or "{}")
    print()
    ui.line("  ┌─ approval required ─────────────────────────────────────────", ui.WARN)
    ui.write("  │ tool  ", ui.WARN)
    ui.line(req.function_call.name, "white")
    ui.write("  │ args  ", ui.WARN)
    ui.line(args[:56] + ("…" if len(args) > 56 else ""), "gray")
    ui.line("  └─────────────────────────────────────────────────────────────", ui.WARN)


if __name__ == "__main__":
    asyncio.run(run())
