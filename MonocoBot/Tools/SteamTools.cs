using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MonocoBot.Configuration;

namespace MonocoBot.Tools;

public class SteamTools
{
    private readonly HttpClient _httpClient;
    private readonly BotOptions _options;

    public SteamTools(HttpClient httpClient, IOptions<BotOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    [Description("Gets the list of owned games for a Steam user by their Steam 64-bit ID. " +
        "The profile must be public. Use ResolveSteamVanityName first if you have a vanity URL instead of a numeric ID.")]
    public async Task<string> GetSteamLibrary(
        [Description("The Steam 64-bit ID (e.g., '76561198012345678')")] string steamId)
    {
        try
        {
            if (string.IsNullOrEmpty(_options.SteamApiKey))
                return "Error: Steam API key is not configured. Set 'Bot:SteamApiKey' in appsettings.json.";

            var url = $"http://api.steampowered.com/IPlayerService/GetOwnedGames/v0001/?key={_options.SteamApiKey}&steamid={steamId}&include_appinfo=1&format=json";
            var response = await _httpClient.GetStringAsync(url);
            var doc = JsonDocument.Parse(response);

            var resp = doc.RootElement.GetProperty("response");
            if (!resp.TryGetProperty("games", out var games))
                return "No games found. The profile may be private. Try GetPrivateProfileGames instead.";

            var gameList = new List<(string Name, double Hours)>();
            foreach (var game in games.EnumerateArray())
            {
                var name = game.GetProperty("name").GetString() ?? "Unknown";
                var playtime = game.GetProperty("playtime_forever").GetInt32();
                gameList.Add((name, playtime / 60.0));
            }

            gameList.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            var totalGames = resp.GetProperty("game_count").GetInt32();

            var lines = gameList.Select(g => $"- {g.Name} ({g.Hours:F1} hours played)");
            return $"**Steam Library** ({totalGames} games):\n{string.Join("\n", lines)}";
        }
        catch (Exception ex)
        {
            return $"Failed to get Steam library: {ex.Message}";
        }
    }

    [Description("Resolves a Steam vanity URL name (the custom URL part) to a numeric Steam 64-bit ID. " +
        "For example, if the profile URL is steamcommunity.com/id/gaben, the vanity name is 'gaben'.")]
    public async Task<string> ResolveSteamVanityName(
        [Description("The vanity URL name (e.g., 'gaben')")] string vanityName)
    {
        try
        {
            if (string.IsNullOrEmpty(_options.SteamApiKey))
                return "Error: Steam API key is not configured.";

            var url = $"http://api.steampowered.com/ISteamUser/ResolveVanityURL/v0001/?key={_options.SteamApiKey}&vanityurl={vanityName}";
            var response = await _httpClient.GetStringAsync(url);
            var doc = JsonDocument.Parse(response);

            var resp = doc.RootElement.GetProperty("response");
            if (resp.GetProperty("success").GetInt32() == 1)
            {
                var steamId = resp.GetProperty("steamid").GetString();
                return $"Resolved '{vanityName}' to Steam ID: {steamId}";
            }

            return $"Could not resolve vanity name '{vanityName}'. It may not exist.";
        }
        catch (Exception ex)
        {
            return $"Failed to resolve vanity name: {ex.Message}";
        }
    }

    [Description("Gets the Steam wishlist for a user. The wishlist must be set to public.")]
    public async Task<string> GetSteamWishlist(
        [Description("The Steam 64-bit ID of the user")] string steamId)
    {
        try
        {
            var url = $"https://store.steampowered.com/wishlist/profiles/{steamId}/wishlistdata/?p=0";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0");

            var response = await _httpClient.SendAsync(request);
            var json = await response.Content.ReadAsStringAsync();

            if (json is "[]" or "" or "null")
                return "Wishlist is empty or private.";

            var doc = JsonDocument.Parse(json);
            var items = new List<string>();

            foreach (var item in doc.RootElement.EnumerateObject())
            {
                if (item.Value.TryGetProperty("name", out var name))
                    items.Add($"- {name.GetString()}");
            }

            items.Sort(StringComparer.OrdinalIgnoreCase);
            return $"**Steam Wishlist** ({items.Count} items):\n{string.Join("\n", items)}";
        }
        catch (Exception ex)
        {
            return $"Failed to get wishlist: {ex.Message}";
        }
    }

    [Description("Searches the bot owner's Steam friend list by display name and returns matching friends with their Steam IDs. " +
        "Use this when a user provides just a friend's name. The returned Steam ID can then be passed to GetSteamLibrary, GetSteamWishlist, etc.")]
    public async Task<string> FindFriendByName(
        [Description("The display name (or part of it) to search for in the friend list")] string friendName)
    {
        try
        {
            if (string.IsNullOrEmpty(_options.SteamApiKey))
                return "Error: Steam API key is not configured.";

            if (string.IsNullOrEmpty(_options.OwnerSteamId))
                return "Error: OwnerSteamId is not configured. Set 'Bot:OwnerSteamId' in appsettings.json.";

            var friends = await GetFriendSteamIds(_options.OwnerSteamId);

            if (friends.Count == 0)
                return "Friend list is empty or private.";

            var summaries = await GetPlayerSummaries(friends);

            var matches = summaries
                .Where(s => s.Name.Contains(friendName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0)
                return $"No friends found matching '{friendName}'. Use GetFriendsList to see all friends.";

            var lines = matches.Select(s =>
                $"- **{s.Name}** — Steam ID: `{s.SteamId}` (profile: {s.ProfileUrl})");

            return $"Found {matches.Count} match(es) for '{friendName}':\n{string.Join("\n", lines)}";
        }
        catch (Exception ex)
        {
            return $"Failed to search friends: {ex.Message}";
        }
    }

    [Description("Lists all friends on the bot owner's Steam friend list with their display names and Steam IDs.")]
    public async Task<string> GetFriendsList()
    {
        try
        {
            if (string.IsNullOrEmpty(_options.SteamApiKey))
                return "Error: Steam API key is not configured.";
            if (string.IsNullOrEmpty(_options.OwnerSteamId))
                return "Error: OwnerSteamId is not configured. Set 'Bot:OwnerSteamId' in appsettings.json.";

            var friends = await GetFriendSteamIds(_options.OwnerSteamId);
            if (friends.Count == 0)
                return "Friend list is empty or private.";

            var summaries = await GetPlayerSummaries(friends);
            summaries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

            var lines = summaries.Select(s => $"- {s.Name}  (`{s.SteamId}`)");
            return $"**Friends List** ({summaries.Count}):\n{string.Join("\n", lines)}";
        }
        catch (Exception ex)
        {
            return $"Failed to get friends list: {ex.Message}";
        }
    }

    [Description("Gets game and wishlist information for a private Steam profile from the locally configured steam_profiles.json file. " +
        "Use this when a Steam profile is set to private and the owner has manually provided their game data.")]
    public string GetPrivateProfileGames(
        [Description("The profile key/name as configured in steam_profiles.json")] string profileName)
    {
        try
        {
            var profilesPath = Path.Combine(AppContext.BaseDirectory, "steam_profiles.json");
            if (!File.Exists(profilesPath))
                return "No steam_profiles.json file found. Ask the server admin to create one with private profile data.";

            var json = File.ReadAllText(profilesPath);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("profiles", out var profiles))
                return "No profiles section found in steam_profiles.json.";

            foreach (var profile in profiles.EnumerateObject())
            {
                if (!string.Equals(profile.Name, profileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var displayName = profile.Value.TryGetProperty("displayName", out var dn) ? dn.GetString() : profile.Name;
                var result = $"**Profile:** {displayName}\n";

                if (profile.Value.TryGetProperty("games", out var games))
                {
                    var gameList = games.EnumerateArray().Select(g => $"- {g.GetString()}").OrderBy(g => g).ToList();
                    result += $"\n**Games ({gameList.Count}):**\n{string.Join("\n", gameList)}";
                }

                if (profile.Value.TryGetProperty("wishlist", out var wishlist))
                {
                    var wishlistItems = wishlist.EnumerateArray().Select(g => $"- {g.GetString()}").OrderBy(g => g).ToList();
                    result += $"\n\n**Wishlist ({wishlistItems.Count}):**\n{string.Join("\n", wishlistItems)}";
                }

                return result;
            }

            var available = string.Join(", ", profiles.EnumerateObject().Select(p => p.Name));
            return $"Profile '{profileName}' not found in steam_profiles.json. Available profiles: {available}";
        }
        catch (Exception ex)
        {
            return $"Failed to read private profile data: {ex.Message}";
        }
    }

    private async Task<List<string>> GetFriendSteamIds(string ownerSteamId)
    {
        var url = $"http://api.steampowered.com/ISteamUser/GetFriendList/v0001/?key={_options.SteamApiKey}&steamid={ownerSteamId}&relationship=friend";
        var response = await _httpClient.GetStringAsync(url);
        var doc = JsonDocument.Parse(response);

        var ids = new List<string>();
        if (doc.RootElement.TryGetProperty("friendslist", out var fl) &&
            fl.TryGetProperty("friends", out var friends))
        {
            foreach (var friend in friends.EnumerateArray())
                ids.Add(friend.GetProperty("steamid").GetString()!);
        }

        return ids;
    }

    private async Task<List<PlayerSummary>> GetPlayerSummaries(List<string> steamIds)
    {
        var summaries = new List<PlayerSummary>();

        // The API accepts up to 100 Steam IDs per call
        foreach (var batch in steamIds.Chunk(100))
        {
            var ids = string.Join(",", batch);
            var url = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={_options.SteamApiKey}&steamids={ids}";
            var response = await _httpClient.GetStringAsync(url);
            var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("response", out var resp) &&
                resp.TryGetProperty("players", out var players))
            {
                foreach (var player in players.EnumerateArray())
                {
                    summaries.Add(new PlayerSummary
                    {
                        SteamId = player.GetProperty("steamid").GetString()!,
                        Name = player.GetProperty("personaname").GetString() ?? "Unknown",
                        ProfileUrl = player.TryGetProperty("profileurl", out var pu) ? pu.GetString() ?? "" : ""
                    });
                }
            }
        }

        return summaries;
    }

    private sealed class PlayerSummary
    {
        public required string SteamId { get; init; }
        public required string Name { get; init; }
        public required string ProfileUrl { get; init; }
    }
}
