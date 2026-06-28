# SPDX-License-Identifier: MIT
# Microsoft Agent Framework — Build & Run the Demo Samples (Python)
"""A tiny, dependency-free console UI toolkit: themed ANSI colors, rounded banners,
per-agent colors, pass/fail badges, and prompts. Mirrors the .NET demo's look."""

from __future__ import annotations

import sys
import warnings

# The framework marks a few subsystems experimental — silence the import-time noise.
# (ui.py is imported before agent_framework in every sample, so this takes effect first.)
warnings.filterwarnings("ignore")


def _setup_console() -> None:
    # UTF-8 output so box-drawing + emoji render (Windows consoles default to cp1252).
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
        except Exception:
            pass
    # Enable ANSI colors on Windows terminals.
    if sys.platform == "win32":
        try:
            import ctypes

            k = ctypes.windll.kernel32
            k.SetConsoleMode(k.GetStdHandle(-11), 7)  # ENABLE_VIRTUAL_TERMINAL_PROCESSING
        except Exception:
            pass


_setup_console()


def _quiet_teardown() -> None:
    # The Hyperlight wasm sandbox (Sample 3) can raise a harmless cross-thread drop error
    # during interpreter shutdown ("WasmSandbox is unsendable…"). Swallow just that noise.
    import threading

    def _is_noise(msg: str) -> bool:
        return "WasmSandbox" in msg or "unsendable" in msg

    _orig_unraisable = sys.unraisablehook

    def _unraisable(args):  # noqa: ANN001
        if _is_noise(str(getattr(args, "exc_value", "") or "")):
            return
        _orig_unraisable(args)

    def _thread_hook(args):  # noqa: ANN001
        if _is_noise(str(getattr(args, "exc_value", "") or "")):
            return
        threading.__excepthook__(args)

    sys.unraisablehook = _unraisable
    threading.excepthook = _thread_hook


_quiet_teardown()

RESET = "\033[0m"
_C = {
    "cyan": "\033[36m",
    "green": "\033[32m",
    "yellow": "\033[33m",
    "magenta": "\033[35m",
    "blue": "\033[34m",
    "red": "\033[31m",
    "white": "\033[97m",
    "silver": "\033[37m",  # light gray — matches .NET ConsoleColor.Gray
    "gray": "\033[90m",
}
ACCENT, MUTED, GOOD, BAD, WARN = "cyan", "gray", "green", "red", "yellow"
WIDTH = 74

_AGENT_COLORS = {
    "Triage": "cyan",
    "Orders": "green",
    "Returns": "yellow",
    "Fraud": "red",
    "Ops": "magenta",
    "ShopBot": "cyan",
    "CodeAct": "magenta",
    "Traditional": "blue",
}
_PALETTE = ["cyan", "green", "yellow", "magenta", "blue", "red"]


def c(text: str, color: str) -> str:
    return f"{_C.get(color, '')}{text}{RESET}"


def write(text: str, color: str) -> None:
    sys.stdout.write(c(text, color))


def line(text: str, color: str) -> None:
    print(c(text, color))


def info(t: str) -> None:
    line("  " + t, "silver")  # light gray, matching .NET Ui.Info


def dim(t: str) -> None:
    line("  " + t, MUTED)


def hint(t: str) -> None:
    line("  💡 " + t, WARN)


def success(t: str) -> None:
    line("  ✅ " + t, GOOD)


def failure(t: str) -> None:
    line("  ❌ " + t, BAD)


def color_for(agent: str | None) -> str:
    if not agent:
        return "gray"
    if agent in _AGENT_COLORS:
        return _AGENT_COLORS[agent]
    return _PALETTE[abs(hash(agent)) % len(_PALETTE)]


def clear() -> None:
    # Only clear a real terminal — keep piped/redirected output intact.
    if sys.stdout.isatty():
        sys.stdout.write("\033[2J\033[3J\033[H")
        sys.stdout.flush()


def banner(title: str, subtitle: str = "") -> None:
    print()
    inner = WIDTH - 2
    line("╭" + "─" * inner + "╮", ACCENT)
    _bline("")
    _bline("  " + title, "white")
    if subtitle:
        _bline("  " + subtitle, MUTED)
    _bline("")
    line("╰" + "─" * inner + "╯", ACCENT)
    print()


def _bline(content: str, color: str = MUTED) -> None:
    inner = WIDTH - 2
    content = content[:inner]
    write("│", ACCENT)
    write(content.ljust(inner), color)
    line("│", ACCENT)


def rule(label: str | None = None) -> None:
    if not label:
        line("─" * WIDTH, MUTED)
    else:
        head = f"── {label} "
        line(head + "─" * max(0, WIDTH - len(head)), MUTED)


def badge(passed: bool, label: str) -> None:
    mark = "  ✔ PASS " if passed else "  ✘ FAIL "
    write(mark, GOOD if passed else BAD)
    line(label, "gray")


def kv(key: str, value: str, value_color: str = "white") -> None:
    write(f"  {key:<18}", MUTED)
    line(value, value_color)


def agent_line(name: str, text: str) -> None:
    """Print a line of agent speech with a colored [name] label."""
    if not text:
        return
    write(f"  [{name}] ", color_for(name))
    print(c(text, "white"))


def prompt(label: str) -> str | None:
    """Prompt for a line of input; returns None on EOF so callers can quit."""
    write(f"\n  {label} ", ACCENT)
    write("› ", "white")
    try:
        return input()
    except EOFError:
        return None


def confirm(question: str) -> bool:
    write(f"\n  {question} ", WARN)
    write("(y/n) › ", "white")
    try:
        ans = input().strip()
    except EOFError:
        return False
    return ans[:1].lower() == "y"


def press_enter() -> None:
    line("\n  Press Enter to return to the menu…", MUTED)
    try:
        input()
    except EOFError:
        pass


class thinking:
    """Static 'thinking…' line (a context manager, no animation — clean when piped)."""

    def __init__(self, label: str):
        self.label = label

    def __enter__(self):
        line(f"  · {self.label}", ACCENT)
        return self

    def __exit__(self, *exc):
        return False
