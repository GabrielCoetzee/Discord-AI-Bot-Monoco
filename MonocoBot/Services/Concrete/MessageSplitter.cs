namespace MonocoBot.Services;

public static class MessageSplitter
{
    public static List<string> Split(string text, int maxLength = Constants.DiscordMaxMessageLength)
    {
        if (string.IsNullOrEmpty(text))
            return [];

        if (text.Length <= maxLength)
            return [text];

        var chunks = new List<string>();
        var remaining = text;

        while (remaining.Length > 0)
        {
            int splitAt;
            if (remaining.Length <= maxLength)
            {
                splitAt = remaining.Length;
            }
            else
            {
                splitAt = remaining.LastIndexOf('\n', maxLength);
                if (splitAt <= 0) splitAt = maxLength;
            }

            chunks.Add(remaining[..splitAt]);
            remaining = remaining[splitAt..].TrimStart('\n');
        }

        return chunks;
    }
}
