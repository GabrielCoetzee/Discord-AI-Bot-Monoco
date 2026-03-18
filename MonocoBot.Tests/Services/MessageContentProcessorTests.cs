using MonocoBot.Services;

namespace MonocoBot.Tests.Services;

public class MessageContentProcessorTests
{
    private readonly MessageContentProcessor _processor = new();

    [Fact]
    public void StripBotMention_RemovesAtMention()
    {
        var result = _processor.StripBotMention("<@123456> hello", 123456);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void StripBotMention_RemovesNicknameMention()
    {
        var result = _processor.StripBotMention("<@!123456> hello", 123456);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void StripBotMention_LeavesOtherMentionsIntact()
    {
        var result = _processor.StripBotMention("<@789> hello", 123456);
        Assert.Equal("<@789> hello", result);
    }

    [Fact]
    public void ResolveUserMentions_ReplacesIdWithDisplayName()
    {
        var mentions = new[] { (Id: 999UL, DisplayName: "Alice") };
        var result = _processor.ResolveUserMentions("hey <@999> what's up", mentions);
        Assert.Equal("hey @Alice (Discord mention: <@999>) what's up", result);
    }

    [Fact]
    public void ResolveUserMentions_ReplacesNicknameFormat()
    {
        var mentions = new[] { (Id: 999UL, DisplayName: "Alice") };
        var result = _processor.ResolveUserMentions("hey <@!999>", mentions);
        Assert.Equal("hey @Alice (Discord mention: <@999>)", result);
    }

    [Fact]
    public void ResolveUserMentions_EmptyMentionsList_ReturnsUnchanged()
    {
        var result = _processor.ResolveUserMentions("hey <@999>", []);
        Assert.Equal("hey <@999>", result);
    }
}
