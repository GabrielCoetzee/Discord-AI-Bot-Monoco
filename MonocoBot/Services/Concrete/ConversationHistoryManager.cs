using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace MonocoBot.Services;

public class ConversationHistoryManager : IConversationHistoryManager
{
    private readonly ConcurrentDictionary<ulong, List<ChatMessage>> _history = new();

    public List<ChatMessage> GetOrCreateHistory(ulong channelId, string systemPrompt)
        => _history.GetOrAdd(channelId, _ => [new ChatMessage(ChatRole.System, systemPrompt)]);

    public void AddMessage(ulong channelId, ChatMessage message)
        => _history.GetOrAdd(channelId, _ => []).Add(message);

    public void ClearHistory(ulong channelId)
        => _history.TryRemove(channelId, out _);

    public void TrimHistory(ulong channelId, int maxHistory)
    {
        if (!_history.TryGetValue(channelId, out var history))
            return;

        while (history.Count > maxHistory + 1)
            history.RemoveAt(1);
    }
}
