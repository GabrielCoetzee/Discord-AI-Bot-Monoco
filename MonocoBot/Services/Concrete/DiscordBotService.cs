using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MonocoBot.Configuration;
using MonocoBot.Tools;

namespace MonocoBot.Services;

public class DiscordBotService : IHostedService
{
    private readonly DiscordSocketClient _discord;
    private readonly IChatClient _chatClient;
    private readonly BotOptions _options;
    private readonly ILogger<DiscordBotService> _logger;
    private readonly ISystemPromptProvider _systemPromptProvider;
    private readonly IConversationHistoryManager _historyManager;
    private readonly IMessageContentProcessor _contentProcessor;
    private readonly IMessageSender _messageSender;
    private readonly IAiToolRegistry _toolRegistry;

    public DiscordBotService(
        DiscordSocketClient discord,
        IChatClient chatClient,
        IOptions<BotOptions> options,
        ILogger<DiscordBotService> logger,
        ISystemPromptProvider systemPromptProvider,
        IConversationHistoryManager historyManager,
        IMessageContentProcessor contentProcessor,
        IMessageSender messageSender,
        IAiToolRegistry toolRegistry)
    {
        _discord = discord;
        _chatClient = chatClient;
        _options = options.Value;
        _logger = logger;
        _systemPromptProvider = systemPromptProvider;
        _historyManager = historyManager;
        _contentProcessor = contentProcessor;
        _messageSender = messageSender;
        _toolRegistry = toolRegistry;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _discord.Log += OnLogAsync;
        _discord.MessageReceived += OnMessageReceivedAsync;
        _discord.Ready += OnReadyAsync;

        await _discord.LoginAsync(TokenType.Bot, _options.DiscordToken);
        await _discord.StartAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _discord.MessageReceived -= OnMessageReceivedAsync;
        await _discord.StopAsync();
    }

    private Task OnReadyAsync()
    {
        _logger.LogInformation("{Name} is online — connected to {Count} server(s).", _options.Name, _discord.Guilds.Count);
        return Task.CompletedTask;
    }

    private Task OnLogAsync(LogMessage log)
    {
        var level = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Trace
        };
        _logger.Log(level, log.Exception, "[Discord] {Message}", log.Message);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(SocketMessage message)
    {
        if (message is not SocketUserMessage userMessage || message.Author.IsBot)
            return;

        var isMentioned = userMessage.MentionedUsers.Any(u => u.Id == _discord.CurrentUser.Id);
        var isDm = message.Channel is IDMChannel;

        if (!isMentioned && !isDm)
            return;

        var content = userMessage.Content;

        if (isMentioned)
            content = _contentProcessor.StripBotMention(content, _discord.CurrentUser.Id);

        var mentionedOthers = userMessage.MentionedUsers
            .Where(u => u.Id != _discord.CurrentUser.Id)
            .Select(u => (u.Id, _contentProcessor.GetAuthorDisplayName(u)));
        content = _contentProcessor.ResolveUserMentions(content, mentionedOthers);

        if (string.IsNullOrEmpty(content))
        {
            await message.Channel.SendMessageAsync($"Hey! I'm **{_options.Name}** \U0001f916. Mention me with a message and I'll help you out!");
            return;
        }

        if (content.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
            content.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            _historyManager.ClearHistory(message.Channel.Id);
            await message.Channel.SendMessageAsync("\U0001f9f9 Conversation history cleared!", messageReference: new MessageReference(message.Id));
            return;
        }

        try
        {
            using var typing = message.Channel.EnterTypingState();

            var systemPrompt = _systemPromptProvider.GetSystemPrompt();
            var history = _historyManager.GetOrCreateHistory(message.Channel.Id, systemPrompt);

            var authorDisplayName = _contentProcessor.GetAuthorDisplayName(message.Author);
            _historyManager.AddMessage(message.Channel.Id, new ChatMessage(ChatRole.User,
                $"[Display Name: {authorDisplayName} | Mention: <@{message.Author.Id}>]: {content}"));

            _historyManager.TrimHistory(message.Channel.Id, _options.MaxConversationHistory);

            ToolOutput.Reset();

            var chatOptions = new ChatOptions
            {
                Tools = [.. _toolRegistry.GetTools()],
                Temperature = _options.AiTemperature
            };

            var response = await _chatClient.GetResponseAsync(history, chatOptions);
            var responseText = response.Text ?? "I couldn't generate a response.";

            _historyManager.AddMessage(message.Channel.Id, new ChatMessage(ChatRole.Assistant, responseText));

            await _messageSender.SendResponseAsync(message.Channel, responseText, ToolOutput.PendingFiles, message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {User}", message.Author.Username);
            await message.Channel.SendMessageAsync(
                $"\u274c Sorry, something went wrong: {ex.Message}",
                messageReference: new MessageReference(message.Id));
        }
    }
}
