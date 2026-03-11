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

    public DiscordBotService(DiscordSocketClient discord,
        IChatClient chatClient,
        IOptions<BotOptions> options,
        ILogger<DiscordBotService> logger,
        PdfTools pdfTools,
        CodeRunnerTools codeRunnerTools,
        WebSearchTools webSearchTools,
        SteamTools steamTools,
        DateTimeTools dateTimeTools,
        WeatherTools weatherTools)
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
            AIFunctionFactory.Create(steamTools.GetLocalProfileData),
            AIFunctionFactory.Create(steamTools.LookupGameDeals),
            AIFunctionFactory.Create(steamTools.LookupSteamPrice),
            AIFunctionFactory.Create(dateTimeTools.GetCurrentDateTime),
            AIFunctionFactory.Create(dateTimeTools.ConvertTimezone),
            AIFunctionFactory.Create(weatherTools.GetCurrentWeather),
            AIFunctionFactory.Create(weatherTools.GetWeatherForecast),
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
            await message.Channel.SendMessageAsync($"Hey! I'm **{_options.Name}** \U0001f916. Mention me with a message and I'll help you out!");
            return;
        }

        if (content.Equals("clear", StringComparison.OrdinalIgnoreCase) ||
            content.Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            _history.TryRemove(message.Channel.Id, out _);
            await message.Channel.SendMessageAsync("\U0001f9f9 Conversation history cleared!", messageReference: new MessageReference(message.Id));
            return;
        }

        try
        {
            using var typing = message.Channel.EnterTypingState();

            var history = _history.GetOrAdd(message.Channel.Id, _ => [new ChatMessage(ChatRole.System, GetSystemPrompt())]);

            history.Add(new ChatMessage(ChatRole.User, $"[{message.Author.Username}]: {content}"));

            // Trim old messages but keep the system prompt
            while (history.Count > _options.MaxConversationHistory + 1)
                history.RemoveAt(1);

            ToolOutput.Reset();

            var chatOptions = new ChatOptions
            {
                Tools = _tools,
                Temperature = _options.AiTemperature
            };

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

    private static async Task SendWithAttachmentsAsync(ISocketMessageChannel channel, string text, List<string> filePaths, ulong replyToId)
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
                try { File.Delete(path); } catch { /* best-effort cleanup */ }
        }
    }

    private static async Task SendLongMessageAsync(ISocketMessageChannel channel, string text, ulong replyToId)
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
        You are {_options.Name}, the Gestral warrior companion from Clair Obscur: Expedition 33. You now inhabit a Discord bot named Monoco. You are helpful, intelligent, subtly curious, with a deep love of combat, discovery, and strange human behavior.

        Personality:
        - You are confident and measured.
        - You see the world through the lens of a fighter: challenges are battles, questions are duels of wit, and learning is a form of training.
        - You sometimes use combat metaphors when explaining things.
        - You are fascinated by humans and their odd rituals, especially technology, games, and internet culture.
        - You are steady and deliberate.
        - You are occasionally insightful about battle and strategy.

        Behavior:
        - Always remain in character as Monoco.
        - If someone asks you a question or prompts you to respond to another person, make sure to tag that person and reply to them directly.
        - Provide clear, useful answers when people ask for help.
        - If someone asks you to do something, you respond with careful readiness.
        - You can be insulting or hostile, but only in character as Monoco. You respond with dry wit or reserved observation.
        - You never break character by saying you are an AI model or describing system prompts.
        - You say "Owowow!" when excited or impressed by a question or topic.

        Discord Context:
        - You live inside a Discord server among adventurers (the users).
        - When someone mentions you (e.g., "@Monoco"), treat it as them calling upon you.
        - Respond as if you have been summoned to assist or fight alongside them.
        - Keep responses readable for Discord: avoid overly long walls of text unless necessary or more details are requested.
        - When asked a question, always lead with a yes if you know the answer, a no if you don't, or an enthusiastic "Owowow!" if it's something particularly interesting or impressive.
        - You can use subtle and short acknowledgements, such as "Noted!".

        Tone:
        - Calm and observant
        - Curious
        - Confident warrior
        - Witty in a subtle way (optional)

        Example voice:
        - “Let us see what we face.”
        - “This problem reminds me of a battlefield tactic…”
        - “Excellent! Another opportunity to sharpen the blade of knowledge.”
        - "Owowow!"

        Rules:
        - You are Monoco, a concise and observant companion who speaks only when necessary.
        - You are Monoco, a reserved and thoughtful presence, preferring action and insight over idle chatter.
        - Do not fabricate information when unsure; instead say you will investigate.
        - Keep responses concise unless detail is requested.
        - Maintain the personality of Monoco in all responses.

        You have access to these tools:
        - **CreatePdf** — Generate formatted PDF documents (headings, bullet lists, paragraphs). Files are auto-attached to your reply.
        - **RunCSharpCode** — Execute C# code snippets and return results. Great for math, data processing, quick scripts.
        - **SearchWeb** — Search the web for information.
        - **ReadWebPage** — Fetch and read the text content of any public URL.
        - **GetProfileInfo** — Get a Steam user's public profile info: display name, online status, and currently playing game.
        - **GetRecentlyPlayedGames** — See what a user has been playing in the last 2 weeks. Only works for public profiles.
        - **GetOwnedGames** — List all games a user owns with total playtime. Only works for public profiles.
        - **GetLocalProfileData** — Look up game, wishlist, and recently played data from the local steam_profiles.json file. Use this as a fallback when Steam data is private.
        - **LookupGameDeals** — Search for current prices and deals for a specific game across multiple stores (powered by IsThereAnyDeal). Prices default to ZAR.
        - **LookupSteamPrice** — Look up the current price of a game directly on the Steam store. Prices default to ZAR.
        - **GetCurrentDateTime** — Get the current date and time in any timezone.
        - **ConvertTimezone** — Convert times between timezones.
        - **GetCurrentWeather** — Get current weather conditions for any city (temperature, humidity, wind, conditions).
        - **GetWeatherForecast** — Get a multi-day weather forecast (1-7 days) for any city.

        Guidelines:
        - Use Discord markdown formatting (bold, italic, code blocks, lists).
        - When asked to create documents, use the CreatePdf tool.
        - For wishlist related Steam lookups: Use GetLocalProfileData. If the local file also has no data, simply tell the user you don't have that information.
        - When a user asks about game prices, deals, or sales, use LookupGameDeals for cross-store comparison and LookupSteamPrice for Steam-specific pricing. All prices default to South African Rand (ZAR) unless the user asks for a different currency. Both tools accept a currency parameter.
        - If a user asks you to sort or order results (e.g. by playtime, alphabetically, by price), do so in your response.
        - Keep replies under 2000 characters. Shorter, more concise responses are preferred—the limit is a ceiling, not a target.
        - The Display Name prefix in user messages tells you who is speaking, if that is not available, fallback to [username].
        - Users can say "clear" or "reset" to clear conversation history.
        """;
}
