using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using MonocoBot.Configuration;
using OpenAI;

namespace MonocoBot.Services;

public static class ChatClientFactory
{
    public static IChatClient Create(BotOptions options)
    {
        var kernelBuilder = Kernel.CreateBuilder();

        switch (options.AiProvider.ToLowerInvariant())
        {
            case "azure":
                kernelBuilder.AddAzureOpenAIChatClient(
                    deploymentName: options.AiModel,
                    endpoint: options.AiEndpoint,
                    apiKey: options.AiApiKey);
                break;

            case "ollama":
                var ollamaClient = new OpenAIClient(
                    new ApiKeyCredential("not-needed"),
                    new OpenAIClientOptions { Endpoint = new Uri(options.AiEndpoint) });
                kernelBuilder.AddOpenAIChatClient(options.AiModel, ollamaClient);
                break;

            default: // "openai"
                kernelBuilder.AddOpenAIChatClient(options.AiModel, options.AiApiKey);
                break;
        }

        var kernel = kernelBuilder.Build();
        var chatClient = kernel.GetRequiredService<IChatClient>();

        return new ChatClientBuilder(chatClient)
            .UseFunctionInvocation()
            .Build();
    }
}
