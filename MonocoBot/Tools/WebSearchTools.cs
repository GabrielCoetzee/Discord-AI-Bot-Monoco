using System.ComponentModel;
using System.Net;
using System.Text.RegularExpressions;
using Flurl.Http;
using HtmlAgilityPack;

namespace MonocoBot.Tools;

public class WebSearchTools
{
    private const string DuckDuckGoUrl = "https://html.duckduckgo.com/html/";

    [Description("Searches the web and returns top results with titles, URLs, and descriptions.")]
    public async Task<string> SearchWeb([Description("The search query")] string query)
    {
        try
        {
            var html = await DuckDuckGoUrl
                .WithHeader("User-Agent", Constants.BrowserUserAgent)
                .WithHeader("Accept", "text/html")
                .WithHeader("Accept-Language", "en-US,en;q=0.9")
                .WithHeader("Referer", "https://html.duckduckgo.com/")
                .PostUrlEncodedAsync(new { q = query })
                .ReceiveString();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var output = $"**Search results for:** {query}\n\n";
            var lines = new List<string>();

            var resultBodies = doc.DocumentNode.SelectNodes("//div[contains(@class, 'result__body')]");
            if (resultBodies is not null)
            {
                foreach (var body in resultBodies.Take(10))
                {
                    var titleNode = body.SelectSingleNode(".//a[contains(@class, 'result__a')]");
                    if (titleNode is null) continue;

                    var title = WebUtility.HtmlDecode(titleNode.InnerText.Trim());
                    var href = titleNode.GetAttributeValue("href", "");
                    var actualUrl = ExtractDdgUrl(href);

                    if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(actualUrl))
                        continue;

                    var snippetNode = body.SelectSingleNode(".//a[contains(@class, 'result__snippet')]");
                    var snippet = snippetNode is not null
                        ? WebUtility.HtmlDecode(snippetNode.InnerText.Trim())
                        : "";

                    if (snippet.Length > 200) snippet = snippet[..200] + "...";

                    var line = $"- [{title}]({actualUrl})";
                    if (!string.IsNullOrEmpty(snippet))
                        line += $"\n  {snippet}";
                    lines.Add(line);
                }
            }

            if (lines.Count > 0)
                output += "**Web Results:**\n" + string.Join("\n", lines);

            return output.Trim().Length > 50 ? output.Trim() : "No meaningful results found. Try a different query.";
        }
        catch (Exception ex)
        {
            return $"Search failed: {ex.Message}";
        }
    }

    [Description("Fetches and extracts the text content of a web page at the given URL. " +
        "Useful for reading articles, documentation, or any public web content.")]
    public async Task<string> ReadWebPage([Description("The full URL of the web page to read (e.g., 'https://example.com')")] string url)
    {
        try
        {
            var html = await url
                .WithHeader("User-Agent", Constants.BrowserUserAgent)
                .GetStringAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

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

    private static string? ExtractDdgUrl(string href)
    {
        if (string.IsNullOrEmpty(href))
            return null;

        if (href.Contains("uddg="))
        {
            try
            {
                var fullUrl = href.StartsWith("//") ? "https:" + href : href;
                var uri = new Uri(fullUrl);
                foreach (var param in uri.Query.TrimStart('?').Split('&'))
                {
                    var parts = param.Split('=', 2);
                    if (parts.Length == 2 && parts[0] == "uddg")
                        return WebUtility.UrlDecode(parts[1]);
                }
            }
            catch { }
        }

        if (href.StartsWith("http"))
            return href;

        return null;
    }
}
