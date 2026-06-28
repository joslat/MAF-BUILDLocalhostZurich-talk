# SPDX-License-Identifier: MIT
# Microsoft Agent Framework — Build & Run the Demo Samples (Python)
"""The one shared requirement for every sample: a chat model.

Every agent talks to a model through a chat client you create once and pass in.
The agent code never changes when you switch providers — swap the client, keep the agent.

Resolution order (first one whose env vars are set wins):
  1. Azure OpenAI  — AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY + AZURE_OPENAI_DEPLOYMENT
  2. OpenAI        — OPENAI_API_KEY (+ optional OPENAI_CHAT_MODEL)

This mirrors the reference AIConfig.cs (Azure OpenAI first), with an OpenAI fallback.
"""

from __future__ import annotations

import os
import warnings

# The framework marks a few subsystems experimental; silence the noise for the demo.
warnings.filterwarnings("ignore")

try:
    from dotenv import load_dotenv

    load_dotenv()
except ImportError:  # python-dotenv is optional
    pass

_backend = "(not resolved)"


def backend_label() -> str:
    return _backend


def _has_azure() -> bool:
    return all(
        os.getenv(v)
        for v in ("AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_API_KEY", "AZURE_OPENAI_DEPLOYMENT")
    )


def is_configured() -> bool:
    return _has_azure() or bool(os.getenv("OPENAI_API_KEY"))


def resolve_backend() -> str:
    """Set + return the backend label from env vars WITHOUT constructing a client."""
    global _backend
    if _has_azure():
        _backend = f"Azure OpenAI · {os.environ['AZURE_OPENAI_DEPLOYMENT']}"
    elif os.getenv("OPENAI_API_KEY"):
        _backend = f"OpenAI · {os.getenv('OPENAI_CHAT_MODEL', 'gpt-4.1-mini')}"
    return _backend


def get_client():
    """Return a chat client from whatever env vars are set."""
    global _backend

    if _has_azure():
        from agent_framework.openai import OpenAIChatCompletionClient

        deployment = os.environ["AZURE_OPENAI_DEPLOYMENT"]
        api_version = os.getenv("AZURE_OPENAI_API_VERSION", "2024-10-21")
        _backend = f"Azure OpenAI · {deployment}"
        # OpenAIChatCompletionClient targets Azure OpenAI when given an azure_endpoint;
        # it uses the chat-completions API, which every Azure deployment supports.
        return OpenAIChatCompletionClient(
            deployment,
            api_key=os.environ["AZURE_OPENAI_API_KEY"],
            azure_endpoint=os.environ["AZURE_OPENAI_ENDPOINT"],
            api_version=api_version,
        )

    if os.getenv("OPENAI_API_KEY"):
        from agent_framework.openai import OpenAIChatClient

        model = os.getenv("OPENAI_CHAT_MODEL", "gpt-4.1-mini")
        _backend = f"OpenAI · {model}"
        return OpenAIChatClient(model)  # reads OPENAI_API_KEY from the environment

    raise SystemExit(
        "No model backend configured. Set AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY + "
        "AZURE_OPENAI_DEPLOYMENT, or OPENAI_API_KEY. See section 1 of the guide."
    )
