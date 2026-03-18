using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using MonocoBot.Configuration;
using MonocoBot.Services;
using MonocoBot.Tools;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

var botSection = builder.Configuration.GetSection("Bot");
var healthPort = botSection.GetValue<int?>("HealthPort") ?? 8080;
builder.WebHost.UseUrls($"http://*:{healthPort}");

builder.Services.Configure<BotOptions>(builder.Configuration.GetSection("Bot"));

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
    ChatClientFactory.Create(sp.GetRequiredService<IOptions<BotOptions>>().Value));

builder.Services.AddSingleton<ISystemPromptProvider>(sp =>
    new SystemPromptProvider(sp.GetRequiredService<IOptions<BotOptions>>().Value.Name));

builder.Services.AddSingleton<IConversationHistoryManager, ConversationHistoryManager>();
builder.Services.AddSingleton<IMessageContentProcessor, MessageContentProcessor>();
builder.Services.AddSingleton<IMessageSender, DiscordMessageSender>();
builder.Services.AddSingleton<IAiToolRegistry, AiToolRegistry>();

builder.Services.AddSingleton<PdfTools>();
builder.Services.AddSingleton<CodeRunnerTools>();
builder.Services.AddSingleton<WebSearchTools>();
builder.Services.AddSingleton<SteamTools>();
builder.Services.AddSingleton<DateTimeTools>();
builder.Services.AddSingleton<WeatherTools>();

builder.Services.AddHostedService<DiscordBotService>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

await app.RunAsync();
