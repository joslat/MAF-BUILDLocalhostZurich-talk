// SPDX-License-Identifier: MIT
// Microsoft Agent Framework — Build & Run the Demo Samples (.NET)

using System.ClientModel;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MafDemos.Infrastructure;

/// <summary>
/// The one shared requirement for every sample: a chat model.
///
/// Every agent talks to a model through an <see cref="IChatClient"/> you create once and pass in.
/// The agent code never changes when you switch providers — <b>swap the client, keep the agent.</b>
///
/// Resolution order (first one whose env vars are set wins):
///   1. Azure OpenAI  — AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY + AZURE_OPENAI_DEPLOYMENT
///   2. OpenAI        — OPENAI_API_KEY (+ optional OPENAI_CHAT_MODEL, default "gpt-4.1-mini")
///
/// This mirrors the reference <c>AIConfig.cs</c> (Azure OpenAI first), with an OpenAI fallback
/// so the samples are portable.
/// </summary>
public static class Chat
{
    /// <summary>A human-readable description of the resolved backend, for the banner.</summary>
    public static string Backend { get; private set; } = "(not resolved)";

    /// <summary>True when a usable backend is configured.</summary>
    public static bool IsConfigured =>
        HasAzure() || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

    /// <summary>
    /// Builds an <see cref="IChatClient"/> from whatever environment variables are set.
    /// Throws a friendly error if nothing is configured.
    /// </summary>
    public static IChatClient CreateClient()
    {
        if (HasAzure())
        {
            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
            var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;
            var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")!;

            Backend = $"Azure OpenAI · {deployment}";
            return new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key))
                .GetChatClient(deployment)
                .AsIChatClient();
        }

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL") ?? "gpt-4.1-mini";
            Backend = $"OpenAI · {model}";
            return new OpenAIClient(new ApiKeyCredential(openAiKey))
                .GetChatClient(model)
                .AsIChatClient();
        }

        throw new InvalidOperationException(
            "No model backend configured. Set AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY + " +
            "AZURE_OPENAI_DEPLOYMENT, or OPENAI_API_KEY. See section 1 of the guide.");
    }

    private static bool HasAzure() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"));
}
