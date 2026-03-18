using Flurl.Http.Testing;
using Microsoft.Extensions.Options;
using MonocoBot.Configuration;
using MonocoBot.Tools;

namespace MonocoBot.Tests.Tools;

public class SteamToolsTests : IDisposable
{
    private readonly HttpTest _httpTest = new();
    private readonly SteamTools _tools;

    public SteamToolsTests()
    {
        var options = Options.Create(new BotOptions { IsThereAnyDealApiKey = "test-key" });
        _tools = new SteamTools(options);
    }

    public void Dispose() => _httpTest.Dispose();

    [Fact]
    public async Task LookupGameDeals_NoResults_ReturnsNotFoundMessage()
    {
        _httpTest.ForCallsTo("https://api.isthereanydeal.com/games/search/*")
            .RespondWithJson(Array.Empty<object>());

        var result = await _tools.LookupGameDeals("NonexistentGame");

        Assert.Contains("No games found", result);
    }

    [Fact]
    public async Task LookupGameDeals_WithDeals_ReturnsFormattedDeals()
    {
        _httpTest.ForCallsTo("https://api.isthereanydeal.com/games/search/*")
            .RespondWithJson(new[]
            {
                new { id = "game1", title = "Test Game", slug = "test-game" }
            });
        _httpTest.ForCallsTo("https://open.er-api.com/*")
            .RespondWithJson(new { rates = new Dictionary<string, double> { ["ZAR"] = 18.5 } });
        _httpTest.ForCallsTo("https://api.isthereanydeal.com/games/prices/*")
            .RespondWithJson(new[]
            {
                new
                {
                    id = "game1",
                    deals = new[]
                    {
                        new
                        {
                            shop = new { name = "Steam" },
                            price = new { amount = 10.0 },
                            regular = new { amount = 20.0 },
                            cut = 50,
                            url = "https://store.steampowered.com/app/123"
                        }
                    }
                }
            });

        var result = await _tools.LookupGameDeals("Test Game", "ZAR");

        Assert.Contains("Test Game", result);
        Assert.Contains("Steam", result);
        Assert.Contains("-50%", result);
    }

    [Fact]
    public async Task LookupSteamPrice_NoResults_ReturnsNotFoundMessage()
    {
        _httpTest.ForCallsTo("https://store.steampowered.com/api/storesearch/*")
            .RespondWithJson(new { total = 0, items = Array.Empty<object>() });

        var result = await _tools.LookupSteamPrice("NonexistentGame");

        Assert.Contains("No games found", result);
    }

    [Fact]
    public async Task LookupSteamPrice_FreeGame_ReturnsFreeToPlay()
    {
        _httpTest.ForCallsTo("https://store.steampowered.com/api/storesearch/*")
            .RespondWithJson(new
            {
                total = 1,
                items = new[] { new { id = 440, name = "Team Fortress 2" } }
            });
        _httpTest.ForCallsTo("https://store.steampowered.com/api/appdetails*")
            .RespondWithJson(new Dictionary<string, object>
            {
                ["440"] = new { success = true, data = new { is_free = true } }
            });

        var result = await _tools.LookupSteamPrice("Team Fortress 2");

        Assert.Contains("Free to Play", result);
    }

    [Fact]
    public void GetLocalProfileData_NoFile_ReturnsNoFileMessage()
    {
        // Run from test directory where steam_profiles.json won't exist
        var result = _tools.GetLocalProfileData("testuser");
        // Either "No steam_profiles.json file found" or "Profile not found" depending on test environment
        Assert.True(
            result.Contains("No steam_profiles.json file found") ||
            result.Contains("not found"),
            $"Unexpected result: {result}");
    }

    [Fact]
    public async Task LookupGameDeals_NoApiKey_ReturnsConfigError()
    {
        var options = Options.Create(new BotOptions { IsThereAnyDealApiKey = "" });
        var tools = new SteamTools(options);

        var result = await tools.LookupGameDeals("any game");

        Assert.Contains("API key is not configured", result);
    }
}
