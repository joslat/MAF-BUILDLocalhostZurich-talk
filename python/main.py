# SPDX-License-Identifier: MIT
# Microsoft Agent Framework — Build & Run the Demo Samples (Python)
#
# A single console app with a menu that runs the on-stage samples:
#   1 Handoff · 2 Evals · 3 CodeAct · 4 Harness approval · 5 ShopBot
#
# Each sample is also runnable on its own, e.g.  python samples/s1_handoff.py
from __future__ import annotations

import asyncio
import importlib
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import ui  # noqa: E402  (imported first: sets UTF-8 + silences import warnings)
from chat import backend_label, is_configured, resolve_backend  # noqa: E402

SAMPLES = [
    ("1", "Handoff you can watch", "Triage routes; Returns escalates one-way to Fraud", "samples.s1_handoff"),
    ("2", "Evals: green vs. red", "Same query, two builds — does each call the tool?", "samples.s2_evals"),
    ("3", "CodeAct head-to-head", "Tool-calling vs. one sandboxed program", "samples.s3_codeact"),
    ("4", "Harness approval gate", 'A risky shell tool needs a human "yes"', "samples.s4_harness"),
    ("5", "ShopBot — the finale", "Cooperate + Act + Tested, in one small app", "samples.s5_shopbot"),
]


def main() -> None:
    if not is_configured():
        ui.banner("Microsoft Agent Framework · Demo Samples", "No model backend configured")
        ui.failure("Set a model backend before running the samples:")
        ui.info("• Azure OpenAI : AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY + AZURE_OPENAI_DEPLOYMENT")
        ui.info("• OpenAI       : OPENAI_API_KEY  (+ optional OPENAI_CHAT_MODEL)")
        return

    resolve_backend()  # set the banner label without building a throwaway client

    while True:
        ui.clear()
        ui.banner("Microsoft Agent Framework · Demo Samples", f"Backend  ·  {backend_label()}")

        for key, title, blurb, _ in SAMPLES:
            ui.write(f"   {key}  ", ui.ACCENT)
            ui.write(f"{title:<26}", "white")
            ui.line(blurb, ui.MUTED)
        ui.write("   q  ", ui.ACCENT)
        ui.line("Quit", "white")

        raw = ui.prompt("choose a sample")
        if raw is None:  # EOF → quit
            break
        choice = raw.strip().lower()
        if choice in ("q", "quit", "exit", "0"):
            break

        picked = next((s for s in SAMPLES if s[0] == choice), None)
        if picked is None:
            ui.dim("  (unknown choice)")
            if sys.stdout.isatty():
                import time

                time.sleep(0.6)  # let the message linger before the menu redraws
            continue

        ui.clear()
        try:
            module = importlib.import_module(picked[3])
            asyncio.run(module.run())
        except KeyboardInterrupt:
            break
        except Exception as ex:  # noqa: BLE001 — one sample's failure shouldn't kill the menu
            ui.rule()
            ui.failure(f"Sample failed: {type(ex).__name__}: {str(ex)[:160]}")
            ui.press_enter()

    ui.line("\n  Thanks for exploring the Microsoft Agent Framework. 👋\n", ui.ACCENT)


if __name__ == "__main__":
    main()
