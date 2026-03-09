using System.ClientModel;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MonocoBot.Configuration;
using MonocoBot.Services;
using MonocoBot.Tools;
using OpenAI;

var builder = Host.CreateApplicationBuilder(args);

// Bind configuration
builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("Bot"));

// Shared HttpClient
builder.Services.AddSingleton<HttpClient>();

// Discord socket client
builder.Services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
                   | GatewayIntents.GuildMessages
                   | GatewayIntents.MessageContent
                   | GatewayIntents.DirectMessages,
    AlwaysDownloadUsers = true
}));

// AI chat client (model-agnostic via Microsoft.Extensions.AI + OpenAI-compatible endpoint)
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<BotOptions>>().Value;

    OpenAIClientOptions? clientOptions = null;

    if (!string.IsNullOrEmpty(opts.AiEndpoint))
        clientOptions = new OpenAIClientOptions { Endpoint = new Uri(opts.AiEndpoint) };

    var apiKey = string.IsNullOrEmpty(opts.AiApiKey) ? "not-needed" : opts.AiApiKey;

    var openAiClient = new OpenAIClient(new ApiKeyCredential(apiKey), clientOptions);

    return new ChatClientBuilder(openAiClient.GetChatClient(opts.AiModel).AsIChatClient())
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
