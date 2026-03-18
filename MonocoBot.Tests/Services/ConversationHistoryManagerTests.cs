using Microsoft.Extensions.AI;
using MonocoBot.Services;

namespace MonocoBot.Tests.Services;

public class ConversationHistoryManagerTests
{
    private readonly ConversationHistoryManager _manager = new();

    [Fact]
    public void GetOrCreateHistory_NewChannel_CreatesHistoryWithSystemPrompt()
    {
        var history = _manager.GetOrCreateHistory(1, "system prompt");
        Assert.Single(history);
        Assert.Equal(ChatRole.System, history[0].Role);
        Assert.Equal("system prompt", history[0].Text);
    }

    [Fact]
    public void GetOrCreateHistory_ExistingChannel_ReturnsSameHistory()
    {
        var h1 = _manager.GetOrCreateHistory(1, "system");
        var h2 = _manager.GetOrCreateHistory(1, "system");
        Assert.Same(h1, h2);
    }

    [Fact]
    public void AddMessage_AppendsToHistory()
    {
        _manager.GetOrCreateHistory(1, "system");
        _manager.AddMessage(1, new ChatMessage(ChatRole.User, "hello"));
        var history = _manager.GetOrCreateHistory(1, "system");
        Assert.Equal(2, history.Count);
        Assert.Equal("hello", history[1].Text);
    }

    [Fact]
    public void ClearHistory_RemovesChannel()
    {
        _manager.GetOrCreateHistory(1, "system");
        _manager.ClearHistory(1);
        // After clear, a new GetOrCreate creates a fresh history
        var history = _manager.GetOrCreateHistory(1, "new system");
        Assert.Single(history);
        Assert.Equal("new system", history[0].Text);
    }

    [Fact]
    public void TrimHistory_PreservesSystemPromptAndMaxMessages()
    {
        _manager.GetOrCreateHistory(1, "system");
        for (int i = 0; i < 10; i++)
            _manager.AddMessage(1, new ChatMessage(ChatRole.User, $"msg{i}"));

        _manager.TrimHistory(1, 5);

        var history = _manager.GetOrCreateHistory(1, "system");
        Assert.Equal(6, history.Count); // system + 5 messages
        Assert.Equal(ChatRole.System, history[0].Role);
    }

    [Fact]
    public void TrimHistory_UnknownChannel_DoesNotThrow()
    {
        var exception = Record.Exception(() => _manager.TrimHistory(999, 5));
        Assert.Null(exception);
    }
}
