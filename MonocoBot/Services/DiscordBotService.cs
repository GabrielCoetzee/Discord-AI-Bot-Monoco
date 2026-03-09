using System.Collections.Concurrent;
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
    private readonly List<AITool> _tools;
    private readonly ConcurrentDictionary<ulong, List<ChatMessage>> _history = new();

    public DiscordBotService(
        DiscordSocketClient discord,
        IChatClient chatClient,
        IOptions<BotOptions> options,
        ILogger<DiscordBotService> logger,
        PdfTools pdfTools,
        CodeRunnerTools codeRunnerTools,
        WebSearchTools webSearchTools,
        SteamTools steamTools,
        DateTimeTools dateTimeTools)
    {
        _discord = discord;
        _chatClient = chatClient;
        _options = options.Value;
        _logger = logger;

        _tools =
        [
            AIFunctionFactory.Create(pdfTools.CreatePdf),
            AIFunctionFactory.Create(codeRunnerTools.RunCSharpCode),
            AIFunctionFactory.Create(webSearchTools.SearchWeb),
            AIFunctionFactory.Create(webSearchTools.ReadWebPage),
            AIFunctionFactory.Create(steamTools.GetSteamLibrary),
            AIFunctionFactory.Create(steamTools.ResolveSteamVanityName),
            AIFunctionFactory.Create(steamTools.GetSteamWishlist),
            AIFunctionFactory.Create(steamTools.FindFriendByName),
            AIFunctionFactory.Create(steamTools.GetFriendsList),
            AIFunctionFactory.Create(steamTools.GetPrivateProfileGames),
            AIFunctionFactory.Create(dateTimeTools.GetCurrentDateTime),
            AIFunctionFactory.Create(dateTimeTools.ConvertTimezone),
        ];
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
        {
            content = content
                .Replace($"<@{_discord.CurrentUser.Id}>", "")
                .Replace($"<@!{_discord.CurrentUser.Id}>", "")
                .Trim();
        }

        if (string.IsNullOrEmpty(content))
        {
            await message.Channel.SendMessageAsync(
                $"Hey! I'm **{_options.Name}** \U0001f916. Mention me with a message and I'll help you out!");
            return;
        }

        // Handle the "clear" command to reset conversation history
        if (content.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
            content.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            _history.TryRemove(message.Channel.Id, out _);
            await message.Channel.SendMessageAsync("\U0001f9f9 Conversation history cleared!",
                messageReference: new MessageReference(message.Id));
            return;
        }

        try
        {
            using var typing = message.Channel.EnterTypingState();

            var history = _history.GetOrAdd(message.Channel.Id, _ =>
                [new ChatMessage(ChatRole.System, GetSystemPrompt())]);

            history.Add(new ChatMessage(ChatRole.User, $"[{message.Author.Username}]: {content}"));

            // Trim old messages but keep the system prompt
            while (history.Count > _options.MaxConversationHistory + 1)
                history.RemoveAt(1);

            ToolOutput.Reset();

            var chatOptions = new ChatOptions { Tools = _tools };
            var response = await _chatClient.GetResponseAsync(history, chatOptions);
            var responseText = response.Text ?? "I couldn't generate a response.";

            history.Add(new ChatMessage(ChatRole.Assistant, responseText));

            var pendingFiles = ToolOutput.PendingFiles;

            if (pendingFiles.Count > 0)
            {
                await SendWithAttachmentsAsync(message.Channel, responseText, pendingFiles, message.Id);
            }
            else
            {
                await SendLongMessageAsync(message.Channel, responseText, message.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {User}", message.Author.Username);
            await message.Channel.SendMessageAsync(
                $"\u274c Sorry, something went wrong: {ex.Message}",
                messageReference: new MessageReference(message.Id));
        }
    }

    private static async Task SendWithAttachmentsAsync(
        ISocketMessageChannel channel, string text, List<string> filePaths, ulong replyToId)
    {
        if (text.Length > 2000)
            text = text[..1997] + "...";

        var attachments = new List<FileAttachment>();
        try
        {
            foreach (var path in filePaths)
            {
                if (File.Exists(path))
                    attachments.Add(new FileAttachment(File.OpenRead(path), Path.GetFileName(path)));
            }

            await channel.SendFilesAsync(attachments, text, messageReference: new MessageReference(replyToId));
        }
        finally
        {
            foreach (var a in attachments)
                a.Dispose();
            foreach (var path in filePaths)
            {
                try { File.Delete(path); } catch { /* best-effort cleanup */ }
            }
        }
    }

    private static async Task SendLongMessageAsync(
        ISocketMessageChannel channel, string text, ulong replyToId)
    {
        const int max = 2000;
        if (text.Length <= max)
        {
            await channel.SendMessageAsync(text, messageReference: new MessageReference(replyToId));
            return;
        }

        var remaining = text;
        var isFirst = true;
        while (remaining.Length > 0)
        {
            int splitAt;
            if (remaining.Length <= max)
            {
                splitAt = remaining.Length;
            }
            else
            {
                splitAt = remaining.LastIndexOf('\n', max);
                if (splitAt <= 0) splitAt = max;
            }

            var chunk = remaining[..splitAt];
            remaining = remaining[splitAt..].TrimStart('\n');

            var reference = isFirst ? new MessageReference(replyToId) : null;
            await channel.SendMessageAsync(chunk, messageReference: reference);
            isFirst = false;
        }
    }

    private string GetSystemPrompt() => $"""
        You are {_options.Name}, a helpful and friendly Discord bot assistant.

        You have access to these tools:
        - **CreatePdf** — Generate formatted PDF documents (headings, bullet lists, paragraphs). Files are auto-attached to your reply.
        - **RunCSharpCode** — Execute C# code snippets and return results. Great for math, data processing, quick scripts.
        - **SearchWeb** — Search the web via DuckDuckGo.
        - **ReadWebPage** — Fetch and read the text content of any public URL.
        - **GetSteamLibrary** — List a user's owned Steam games (public profiles only, needs Steam 64-bit ID).
        - **ResolveSteamVanityName** — Convert a Steam vanity URL name to a numeric Steam ID.
        - **GetSteamWishlist** — List a user's Steam wishlist (public only).
        - **FindFriendByName** — Search the owner's Steam friend list by display name to get their Steam ID. Use this when someone says just a name like "check my friend John's wishlist".
        - **GetFriendsList** — List all friends on the owner's Steam friend list.
        - **GetPrivateProfileGames** — Look up manually-provided game data for private Steam profiles from steam_profiles.json.
        - **GetCurrentDateTime** — Get the current date and time in any timezone.
        - **ConvertTimezone** — Convert times between timezones.

        Guidelines:
        - Be friendly, concise, and helpful.
        - Use Discord markdown formatting (bold, italic, code blocks, lists).
        - When asked to create documents, use the CreatePdf tool.
        - For Steam lookups: if a user refers to a friend by display name, use FindFriendByName first to get their Steam ID, then use GetSteamLibrary/GetSteamWishlist with that ID. If a profile is private, try GetPrivateProfileGames.
        - Keep text replies under 2000 characters when possible.
        - The [username] prefix in user messages tells you who is speaking.
        - Users can say "clear" or "reset" to clear conversation history.
        """;
}
