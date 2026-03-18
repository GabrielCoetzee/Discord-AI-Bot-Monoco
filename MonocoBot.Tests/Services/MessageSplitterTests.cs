using MonocoBot.Services;

namespace MonocoBot.Tests.Services;

public class MessageSplitterTests
{
    [Fact]
    public void Split_EmptyString_ReturnsEmpty()
    {
        var result = MessageSplitter.Split("");
        Assert.Empty(result);
    }

    [Fact]
    public void Split_NullString_ReturnsEmpty()
    {
        var result = MessageSplitter.Split(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void Split_ShortText_ReturnsSingleChunk()
    {
        var text = "Hello, world!";
        var result = MessageSplitter.Split(text, 2000);
        Assert.Single(result);
        Assert.Equal(text, result[0]);
    }

    [Fact]
    public void Split_ExactBoundary_ReturnsSingleChunk()
    {
        var text = new string('a', 2000);
        var result = MessageSplitter.Split(text, 2000);
        Assert.Single(result);
    }

    [Fact]
    public void Split_OneBeyondBoundary_ReturnsTwoChunks()
    {
        var text = new string('a', 2001);
        var result = MessageSplitter.Split(text, 2000);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Split_SplitsOnNewlineBoundary()
    {
        var line1 = new string('a', 1900);
        var line2 = new string('b', 200);
        var text = line1 + "\n" + line2;
        var result = MessageSplitter.Split(text, 2000);
        Assert.Equal(2, result.Count);
        Assert.Equal(line1, result[0]);
        Assert.Equal(line2, result[1]);
    }

    [Fact]
    public void Split_NoNewlineInLongText_SplitsAtMaxLength()
    {
        var text = new string('a', 4500);
        var result = MessageSplitter.Split(text, 2000);
        Assert.Equal(3, result.Count);
        Assert.All(result, chunk => Assert.True(chunk.Length <= 2000));
    }

    [Fact]
    public void Split_MultipleChunks_PreservesAllText()
    {
        var text = string.Join("\n", Enumerable.Repeat(new string('x', 500), 10));
        var result = MessageSplitter.Split(text, 2000);
        Assert.True(result.Count > 1);
        Assert.Equal(text.Replace("\n", ""), string.Join("", result.Select(c => c.Replace("\n", ""))));
    }
}
