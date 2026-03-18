using Discord.WebSocket;

namespace MonocoBot.Services;

public interface IMessageSender
{
    Task SendResponseAsync(ISocketMessageChannel channel, string text, List<string> filePaths, ulong replyToId);
}
