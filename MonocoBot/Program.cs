using System.ClientModel;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using MonocoBot.Configuration;
using MonocoBot.Services;
using MonocoBot.Tools;
using OpenAI;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("Bot"));

builder.Services.AddSingleton<HttpClient>();

// Discord socket client
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
                   | GatewayIntents.GuildMessages 
                   | GatewayIntents.GuildMembers
                   | GatewayIntents.MessageContent
                   | GatewayIntents.DirectMessages,

    AlwaysDownloadUsers = true
}));

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<BotOptions>>().Value;
    var kernelBuilder = Kernel.CreateBuilder();

    switch (opts.AiProvider.ToLowerInvariant())
    {
        case "azure":
            kernelBuilder.AddAzureOpenAIChatClient(
                deploymentName: opts.AiModel,
                endpoint: opts.AiEndpoint,
                apiKey: opts.AiApiKey);
            break;

        case "ollama":
            var ollamaClient = new OpenAIClient(
                new ApiKeyCredential("not-needed"),
                new OpenAIClientOptions { Endpoint = new Uri(opts.AiEndpoint) });
            kernelBuilder.AddOpenAIChatClient(opts.AiModel, ollamaClient);
            break;

        default: // "openai"
            kernelBuilder.AddOpenAIChatClient(opts.AiModel, opts.AiApiKey);
            break;
    }

    var kernel = kernelBuilder.Build();
    var chatClient = kernel.GetRequiredService<IChatClient>();

    return new ChatClientBuilder(chatClient)
        .UseFunctionInvocation()
        .Build();
});

builder.Services.AddSingleton<PdfTools>();
builder.Services.AddSingleton<CodeRunnerTools>();
builder.Services.AddSingleton<WebSearchTools>();
builder.Services.AddSingleton<SteamTools>();
builder.Services.AddSingleton<DateTimeTools>();

builder.Services.AddHostedService<DiscordBotService>();

var app = builder.Build();
await app.RunAsync();
