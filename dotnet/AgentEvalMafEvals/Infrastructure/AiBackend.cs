// SPDX-License-Identifier: MIT
// AgentEval × MAF — resolves the one shared dependency: a chat model.

using System.ClientModel;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using OpenAI;

namespace AgentEvalMafEvals.Infrastructure;

/// <summary>
/// Resolves an <see cref="IChatClient"/> from environment variables (Azure OpenAI first, then OpenAI).
/// The same client backs both the agent under test and the LLM-as-judge AgentEval's quality metrics
/// use — swap the client, keep the agent.
/// </summary>
public sealed class AiBackend
{
    public required IChatClient Chat { get; init; }
    public required string ModelId { get; init; }
    public required string Backend { get; init; }

    /// <summary>The judge configuration MAF's evaluator + AgentEval's LLM metrics consume.</summary>
    public ChatConfiguration JudgeConfig => new(Chat);

    public static bool IsConfigured =>
        HasAzure() || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));

    public static AiBackend Resolve()
    {
        if (HasAzure())
        {
            var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!;
            var key = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!;
            var deployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")!;

            var chat = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key))
                .GetChatClient(deployment)
                .AsIChatClient();
            return new AiBackend { Chat = chat, ModelId = deployment, Backend = $"Azure OpenAI · {deployment}" };
        }

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            ?? throw new InvalidOperationException(
                "No model backend configured. Set AZURE_OPENAI_ENDPOINT + AZURE_OPENAI_API_KEY + " +
                "AZURE_OPENAI_DEPLOYMENT, or OPENAI_API_KEY.");
        var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL") ?? "gpt-4.1-mini";

        var oaChat = new OpenAIClient(new ApiKeyCredential(openAiKey)).GetChatClient(model).AsIChatClient();
        return new AiBackend { Chat = oaChat, ModelId = model, Backend = $"OpenAI · {model}" };
    }

    private static bool HasAzure() =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT"));
}
