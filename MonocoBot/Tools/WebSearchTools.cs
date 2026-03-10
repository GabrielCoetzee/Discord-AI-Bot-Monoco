using System.ComponentModel;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace MonocoBot.Tools;

public class WebSearchTools
{
    private readonly HttpClient _httpClient;

    public WebSearchTools(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [Description("Searches the web using DuckDuckGo and returns top results with titles, URLs, and a summary if available.")]
    public async Task<string> SearchWeb(
        [Description("The search query")] string query)
    {
        try
        {
            var encoded = WebUtility.UrlEncode(query);

            var instantUrl = $"https://api.duckduckgo.com/?q={encoded}&format=json&no_html=1";
            var instantRequest = new HttpRequestMessage(HttpMethod.Get, instantUrl);
            instantRequest.Headers.Add("User-Agent", "MonocoBot/1.0");
            var instantResponse = await _httpClient.SendAsync(instantRequest);
            var instantJson = await instantResponse.Content.ReadAsStringAsync();
            var jsonDoc = JsonDocument.Parse(instantJson);

            string? abstractText = null;
            var relatedTopics = new List<string>();

            if (jsonDoc.RootElement.TryGetProperty("AbstractText", out var at) && at.GetString() is { Length: > 0 } abs)
                abstractText = abs;

            if (jsonDoc.RootElement.TryGetProperty("RelatedTopics", out var rt))
            {
                foreach (var topic in rt.EnumerateArray().Take(8))
                {
                    if (topic.TryGetProperty("Text", out var text) && topic.TryGetProperty("FirstURL", out var url))
                    {
                        var t = text.GetString() ?? "";
                        if (t.Length > 150) t = t[..150] + "...";
                        relatedTopics.Add($"- {t}\n  {url.GetString()}");
                    }
                }
            }

            var liteUrl = $"https://lite.duckduckgo.com/lite?q={encoded}";
            var liteRequest = new HttpRequestMessage(HttpMethod.Get, liteUrl);
            liteRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            var liteResponse = await _httpClient.SendAsync(liteRequest);
            var html = await liteResponse.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var searchResults = new List<string>();
            var links = doc.DocumentNode.SelectNodes("//a[starts-with(@href, 'http')]");
            if (links is not null)
            {
                foreach (var link in links.Take(10))
                {
                    var href = link.GetAttributeValue("href", "");
                    var text = link.InnerText.Trim();
                    if (!string.IsNullOrEmpty(text) && !href.Contains("duckduckgo.com"))
                        searchResults.Add($"- [{text}]({href})");
                }
            }

            var output = $"**Search results for:** {query}\n\n";
            if (!string.IsNullOrEmpty(abstractText))
                output += $"**Summary:** {abstractText}\n\n";
            if (relatedTopics.Count > 0)
                output += "**Related:**\n" + string.Join("\n", relatedTopics) + "\n\n";
            if (searchResults.Count > 0)
                output += "**Web Results:**\n" + string.Join("\n", searchResults);

            return output.Trim().Length > 50 ? output.Trim() : "No meaningful results found. Try a different query.";
        }
        catch (Exception ex)
        {
            return $"Search failed: {ex.Message}";
        }
    }

    [Description("Fetches and extracts the text content of a web page at the given URL. " +
        "Useful for reading articles, documentation, or any public web content.")]
    public async Task<string> ReadWebPage(
        [Description("The full URL of the web page to read (e.g., 'https://example.com')")] string url)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var html = await response.Content.ReadAsStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove non-content elements
            foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//nav|//footer|//header|//aside|//noscript") ?? Enumerable.Empty<HtmlNode>())
                node.Remove();

            var text = doc.DocumentNode.InnerText;
            text = WebUtility.HtmlDecode(text);
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n\s*\n+", "\n\n");
            text = text.Trim();

            if (text.Length > 4000)
                text = text[..4000] + "\n\n... (content truncated)";

            return string.IsNullOrEmpty(text) ? "Could not extract text content from the page." : text;
        }
        catch (Exception ex)
        {
            return $"Failed to read web page: {ex.Message}";
        }
    }
}
