using Discord;

namespace MonocoBot.Services;

public interface IMessageContentProcessor
{
    string StripBotMention(string content, ulong botUserId);
    string ResolveUserMentions(string content, IEnumerable<(ulong Id, string DisplayName)> mentionedUsers);
    string GetAuthorDisplayName(IUser author);
}
