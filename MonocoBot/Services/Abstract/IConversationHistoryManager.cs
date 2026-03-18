using Microsoft.Extensions.AI;

namespace MonocoBot.Services;

public interface IConversationHistoryManager
{
    List<ChatMessage> GetOrCreateHistory(ulong channelId, string systemPrompt);
    void AddMessage(ulong channelId, ChatMessage message);
    void ClearHistory(ulong channelId);
    void TrimHistory(ulong channelId, int maxHistory);
}
