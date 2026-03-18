using Discord;
using Discord.WebSocket;

namespace MonocoBot.Services;

public class DiscordMessageSender : IMessageSender
{
    public async Task SendResponseAsync(ISocketMessageChannel channel, string text, List<string> filePaths, ulong replyToId)
    {
        if (filePaths.Count > 0)
            await SendWithAttachmentsAsync(channel, text, filePaths, replyToId);
        else
            await SendLongMessageAsync(channel, text, replyToId);
    }

    private static async Task SendWithAttachmentsAsync(ISocketMessageChannel channel, string text, List<string> filePaths, ulong replyToId)
    {
        if (text.Length > Constants.DiscordMaxMessageLength)
            text = text[..(Constants.DiscordMaxMessageLength - 3)] + "...";

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
        var chunks = MessageSplitter.Split(text);
        var isFirst = true;

        foreach (var chunk in chunks)
        {
            var reference = isFirst ? new MessageReference(replyToId) : null;
            await channel.SendMessageAsync(chunk, messageReference: reference);
            isFirst = false;
        }
    }
}
