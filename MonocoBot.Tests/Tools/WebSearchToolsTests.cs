using Flurl.Http.Testing;
using MonocoBot.Tools;

namespace MonocoBot.Tests.Tools;

public class WebSearchToolsTests : IDisposable
{
    private readonly HttpTest _httpTest = new();
    private readonly WebSearchTools _tools = new();

    public void Dispose() => _httpTest.Dispose();

    [Fact]
    public async Task SearchWeb_WithResults_ReturnsFormattedResults()
    {
        _httpTest.ForCallsTo("https://html.duckduckgo.com/*")
            .RespondWith("""
                <html><body>
                <div class="result__body">
                  <a class="result__a" href="https://example.com">Example Title</a>
                  <a class="result__snippet">A great example snippet.</a>
                </div>
                </body></html>
                """, 200, null, "text/html");

        var result = await _tools.SearchWeb("test query");

        Assert.Contains("test query", result);
        Assert.Contains("Example Title", result);
    }

    [Fact]
    public async Task SearchWeb_NoResults_ReturnsNoResultsMessage()
    {
        _httpTest.ForCallsTo("https://html.duckduckgo.com/*")
            .RespondWith("<html><body></body></html>", 200, null, "text/html");

        var result = await _tools.SearchWeb("obscure query xyz");

        Assert.Contains("No meaningful results found", result);
    }

    [Fact]
    public async Task SearchWeb_HttpError_ReturnsErrorMessage()
    {
        _httpTest.ForCallsTo("https://html.duckduckgo.com/*")
            .RespondWith(status: 500);

        var result = await _tools.SearchWeb("test");

        Assert.Contains("Search failed", result);
    }

    [Fact]
    public async Task ReadWebPage_ValidHtml_ReturnsExtractedText()
    {
        _httpTest.ForCallsTo("https://example.com")
            .RespondWith("<html><body><p>Hello world content.</p></body></html>", 200, null, "text/html");

        var result = await _tools.ReadWebPage("https://example.com");

        Assert.Contains("Hello world content", result);
    }

    [Fact]
    public async Task ReadWebPage_LongContent_TruncatesAt4000()
    {
        var longText = new string('x', 5000);
        _httpTest.ForCallsTo("*")
            .RespondWith($"<html><body><p>{longText}</p></body></html>", 200, null, "text/html");

        var result = await _tools.ReadWebPage("https://example.com");

        Assert.Contains("content truncated", result);
        Assert.True(result.Length <= 4100); // allows for truncation message
    }

    [Fact]
    public async Task ReadWebPage_HttpError_ReturnsErrorMessage()
    {
        _httpTest.ForCallsTo("*")
            .RespondWith(status: 404);

        var result = await _tools.ReadWebPage("https://example.com/notfound");

        Assert.Contains("Failed to read web page", result);
    }
}
