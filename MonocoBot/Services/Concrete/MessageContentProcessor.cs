using Discord;
using Discord.WebSocket;

namespace MonocoBot.Services;

public class MessageContentProcessor : IMessageContentProcessor
{
    public string StripBotMention(string content, ulong botUserId)
        => content
            .Replace($"<@{botUserId}>", "")
            .Replace($"<@!{botUserId}>", "")
            .Trim();

    public string ResolveUserMentions(string content, IEnumerable<(ulong Id, string DisplayName)> mentionedUsers)
    {
        foreach (var (id, displayName) in mentionedUsers)
        {
            content = content
                .Replace($"<@{id}>", $"@{displayName} (Discord mention: <@{id}>)")
                .Replace($"<@!{id}>", $"@{displayName} (Discord mention: <@{id}>)");
        }

        return content;
    }

    public string GetAuthorDisplayName(IUser author)
    {
        if (author is SocketGuildUser guildUser && !string.IsNullOrWhiteSpace(guildUser.DisplayName))
            return guildUser.DisplayName;

        return author.Username;
    }
}
