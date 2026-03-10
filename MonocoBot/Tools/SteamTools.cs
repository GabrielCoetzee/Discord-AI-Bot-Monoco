using System.ComponentModel;
using System.Text.Json;

namespace MonocoBot.Tools;

public class SteamTools
{
    private readonly HttpClient _httpClient;

    public SteamTools(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ── Tool Methods ────────────────────────────────────────────────────

    [Description("Extracts a numeric 64-bit Steam ID from a Steam profile URL. " +
        "Accepts a full profile URL (e.g. 'steamcommunity.com/profiles/76561198012345678') or a raw 64-bit ID. " +
        "Note: vanity name resolution (e.g. 'steamcommunity.com/id/gaben') is not supported — " +
        "ask the user for their full profile URL with the numeric ID instead.")]
    public string ResolveSteamId(
        [Description("A Steam profile URL or 64-bit Steam ID")] string profileInput)
    {
        var input = profileInput.Trim().TrimEnd('/');

        if (input.Contains("steamcommunity.com", StringComparison.OrdinalIgnoreCase))
        {
            if (!input.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                input = "https://" + input;

            try
            {
                var uri = new Uri(input);
                var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

                if (segments is ["profiles", var id, ..] && id.Length >= 15 && id.All(char.IsDigit))
                    return $"Steam ID: {id}";

                if (segments is ["id", ..])
                    return "This is a vanity URL. Ask the user for their Steam profile URL that contains a numeric ID " +
                           "(steamcommunity.com/profiles/NUMBERS), or check the local profiles file with GetLocalProfileData.";
            }
            catch
            {
                return "Could not parse the Steam profile URL.";
            }
        }

        if (input.Length >= 15 && input.All(char.IsDigit))
            return $"Steam ID: {input}";

        return "Could not extract a Steam ID. Ask the user for their Steam profile URL " +
               "(steamcommunity.com/profiles/NUMBERS), or try GetLocalProfileData to check the local profiles file.";
    }

    [Description("Gets a Steam user's public wishlist. " +
        "Only works if the wishlist is set to Public in Steam privacy settings. " +
        "If not accessible, try GetLocalProfileData to check the local profiles file instead.")]
    public async Task<string> GetWishlist(
        [Description("The Steam 64-bit ID")] string steamId)
    {
        try
        {
            var items = new List<(uint AppId, string Name)>();
            var page = 0;

            while (true)
            {
                var url = $"https://store.steampowered.com/wishlist/profiles/{steamId}/wishlistdata/?p={page}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("User-Agent", "Mozilla/5.0");

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(json) || json is "[]" or "null")
                    break;

                var doc = JsonDocument.Parse(json);
                var pageItems = 0;

                foreach (var entry in doc.RootElement.EnumerateObject())
                {
                    if (uint.TryParse(entry.Name, out var appId))
                    {
                        var name = entry.Value.TryGetProperty("name", out var n)
                            ? n.GetString() ?? $"App {appId}"
                            : $"App {appId}";
                        items.Add((appId, name));
                        pageItems++;
                    }
                }

                if (pageItems == 0)
                    break;

                page++;
            }

            if (items.Count == 0)
                return "Could not access this user's wishlist. It may be set to private or the profile doesn't exist. " +
                       "Try GetLocalProfileData to check the local profiles file instead.";

            var names = items
                .Select(i => i.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return $"**Steam Wishlist** ({names.Count} items):\n{string.Join("\n", names.Select(n => $"- {n}"))}";
        }
        catch (Exception ex)
        {
            return $"Failed to get wishlist: {ex.Message}";
        }
    }

    [Description("Looks up game, wishlist, and recently played data from the local steam_profiles.json file. " +
        "Use this as a fallback when Steam data can't be fetched (e.g. private profile, no Steam ID available). " +
        "Returns whatever data is available for the given profile name.")]
    public string GetLocalProfileData(
        [Description("The profile name/key as configured in steam_profiles.json (e.g. a Discord username or nickname)")] string profileName)
    {
        try
        {
            var profilesPath = Path.Combine(AppContext.BaseDirectory, "steam_profiles.json");
            if (!File.Exists(profilesPath))
                return "No steam_profiles.json file found.";

            var json = File.ReadAllText(profilesPath);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("profiles", out var profiles))
                return "No profiles configured in steam_profiles.json.";

            foreach (var profile in profiles.EnumerateObject())
            {
                if (!string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var displayName = profile.Value.TryGetProperty("displayName", out var dn)
                    ? dn.GetString() : profile.Name;

                var result = $"**Profile:** {displayName}\n";

                if (profile.Value.TryGetProperty("games", out var games))
                {
                    var gameList = games.EnumerateArray()
                        .Select(g => $"- {g.GetString()}")
                        .OrderBy(g => g)
                        .ToList();
                    result += $"\n**Games ({gameList.Count}):**\n{string.Join("\n", gameList)}";
                }

                if (profile.Value.TryGetProperty("wishlist", out var wishlist))
                {
                    var wishlistItems = wishlist.EnumerateArray()
                        .Select(g => $"- {g.GetString()}")
                        .OrderBy(g => g)
                        .ToList();
                    result += $"\n\n**Wishlist ({wishlistItems.Count}):**\n{string.Join("\n", wishlistItems)}";
                }

                if (profile.Value.TryGetProperty("recentlyPlayed", out var recent))
                {
                    var recentItems = recent.EnumerateArray()
                        .Select(g => $"- {g.GetString()}")
                        .ToList();
                    result += $"\n\n**Recently Played ({recentItems.Count}):**\n{string.Join("\n", recentItems)}";
                }

                return result;
            }

            var available = string.Join(", ", profiles.EnumerateObject().Select(p => p.Name));
            return available.Length > 0
                ? $"Profile '{profileName}' not found in steam_profiles.json. Available profiles: {available}"
                : $"Profile '{profileName}' not found and no profiles are configured in steam_profiles.json.";
        }
        catch (Exception ex)
        {
            return $"Failed to read local profile data: {ex.Message}";
        }
    }

    [Description("Looks up pricing, deals, and sale information for a specific game by searching across multiple stores. " +
        "Use this to find current prices, discounts, or where a game is cheapest.")]
    public async Task<string> LookupGameDeals(
        [Description("The name of the game to look up deals for")] string gameName)
    {
        try
        {
            var encoded = System.Net.WebUtility.UrlEncode(gameName);
            var url = $"https://www.cheapshark.com/api/1.0/games?title={encoded}&limit=5";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "MonocoBot/1.0");
            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            var results = JsonDocument.Parse(json).RootElement;
            if (results.GetArrayLength() == 0)
                return $"No deals found for '{gameName}'.";

            var lines = new List<string>();
            foreach (var game in results.EnumerateArray())
            {
                var title = game.GetProperty("external").GetString() ?? "Unknown";
                var cheapest = game.GetProperty("cheapest").GetString() ?? "?";
                var gameId = game.GetProperty("gameID").GetString();

                var dealLine = $"- **{title}** — cheapest: **${cheapest}**";

                if (gameId is not null)
                    dealLine += $" ([view deals](https://www.cheapshark.com/redirect?gameID={gameId}))";

                lines.Add(dealLine);
            }

            return $"**Deals for \"{gameName}\":**\n{string.Join("\n", lines)}";
        }
        catch (Exception ex)
        {
            return $"Failed to look up game deals: {ex.Message}";
        }
    }
}
