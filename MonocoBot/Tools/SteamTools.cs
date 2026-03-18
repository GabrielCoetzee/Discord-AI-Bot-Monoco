using System.ComponentModel;
using System.Text.Json;
using Flurl.Http;
using Microsoft.Extensions.Options;
using MonocoBot.Configuration;
using MonocoBot.Models.Steam;

namespace MonocoBot.Tools;

public class SteamTools
{
    private const string ItadBaseUrl = "https://api.isthereanydeal.com";
    private const string SteamStoreBaseUrl = "https://store.steampowered.com/api";

    private readonly string _itadApiKey;

    public SteamTools(IOptions<BotOptions> options)
    {
        _itadApiKey = options.Value.IsThereAnyDealApiKey;
    }

    [Description("Looks up game, wishlist, and recently played data from the local steam_profiles.json file. " +
        "Use this as a fallback when Steam data can't be fetched (e.g. private profile, no Steam ID available). " +
        "Returns whatever data is available for the given profile name.")]
    public string GetLocalProfileData([Description("The profile name/key as configured in steam_profiles.json (e.g. a Discord username or nickname)")] string profileName)
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

    [Description("Looks up pricing, deals, and sale information for a specific game by searching across multiple stores using IsThereAnyDeal. " +
        "Use this to find current prices, discounts, or where a game is cheapest. " +
        "Prices default to South African Rand (ZAR). Specify a different currency code if needed.")]
    public async Task<string> LookupGameDeals(
        [Description("The name of the game to look up deals for")] string gameName,
        [Description("Currency code for prices (default: ZAR). Examples: USD, EUR, GBP, CAD, AUD")] string currency = "ZAR")
    {
        if (string.IsNullOrWhiteSpace(_itadApiKey))
            return "IsThereAnyDeal API key is not configured. Add it to the Bot:IsThereAnyDealApiKey setting.";

        try
        {
            currency = currency.ToUpperInvariant();

            var searchResults = await $"{ItadBaseUrl}/games/search/v1"
                .WithHeader("User-Agent", Constants.BotUserAgent)
                .SetQueryParams(new { title = gameName, results = 5, key = _itadApiKey })
                .GetJsonAsync<List<ItadSearchResult>>();

            if (searchResults.Count == 0)
                return $"No games found matching '{gameName}' on IsThereAnyDeal.";

            var gameIds = searchResults.Select(g => g.Id).ToList();
            var titleMap = searchResults.ToDictionary(g => g.Id, g => g.Title);
            var slugMap = searchResults.ToDictionary(g => g.Id, g => g.Slug ?? "");

            var pricesResults = await $"{ItadBaseUrl}/games/prices/v2"
                .WithHeader("User-Agent", Constants.BotUserAgent)
                .SetQueryParams(new { key = _itadApiKey, country = "US", capacity = 3 })
                .PostJsonAsync(gameIds)
                .ReceiveJson<List<ItadPriceResult>>();

            double exchangeRate;
            string displayCurrency;
            if (currency == "USD")
            {
                exchangeRate = 1.0;
                displayCurrency = "USD";
            }
            else
            {
                var rate = await CurrencyHelper.GetExchangeRateAsync("USD", currency);
                (exchangeRate, displayCurrency) = rate is not null ? (rate.Value, currency) : (1.0, "USD");
            }

            var lines = pricesResults.Select(entry =>
            {
                var title = titleMap.GetValueOrDefault(entry.Id, "Unknown");
                var slug = slugMap.GetValueOrDefault(entry.Id, "");
                var itadLink = !string.IsNullOrEmpty(slug) ? $" ([view on ITAD](https://isthereanydeal.com/game/{slug}/info/))" : "";

                if (entry.Deals is null || entry.Deals.Count == 0)
                    return $"- **{title}** — no current deals";

                var dealLines = entry.Deals.Select(deal => FormatDeal(deal, exchangeRate, displayCurrency));
                return $"- **{title}**{itadLink}\n{string.Join("\n", dealLines)}";
            }).ToList();

            return lines.Count > 0
                ? $"**Deals for \"{gameName}\" ({displayCurrency}):**\n{string.Join("\n", lines)}"
                : $"No deals found for '{gameName}'.";
        }
        catch (Exception ex)
        {
            return $"Failed to look up game deals: {ex.Message}";
        }
    }

    [Description("Looks up the current price of a game directly on the Steam store. " +
        "Prices default to South African Rand (ZAR). Specify a different currency code if needed (e.g. USD, EUR, GBP).")]
    public async Task<string> LookupSteamPrice(
        [Description("The name of the game to look up on Steam")] string gameName,
        [Description("Currency code for prices (default: ZAR). Examples: USD, EUR, GBP, CAD, AUD")] string currency = "ZAR")
    {
        try
        {
            currency = currency.ToUpperInvariant();
            var cc = CurrencyHelper.GetSteamCountryCode(currency);

            var searchDoc = await $"{SteamStoreBaseUrl}/storesearch/"
                .WithHeader("User-Agent", Constants.BotUserAgent)
                .SetQueryParams(new { term = gameName, l = "english", cc })
                .GetJsonAsync<SteamSearchResponse>();

            if (searchDoc.Total == 0)
                return $"No games found matching '{gameName}' on Steam.";

            var lines = new List<string>();

            foreach (var item in searchDoc.Items.Take(5))
            {
                var storeUrl = $"https://store.steampowered.com/app/{item.Id}";

                var detailsResponse = await $"{SteamStoreBaseUrl}/appdetails"
                    .WithHeader("User-Agent", Constants.BotUserAgent)
                    .SetQueryParams(new { appids = item.Id, cc })
                    .GetJsonAsync<Dictionary<string, SteamAppDetailsResponse>>();

                if (!detailsResponse.TryGetValue(item.Id.ToString(), out var appDetails) || !appDetails.Success || appDetails.Data is null)
                {
                    lines.Add($"- **{item.Name}** — details unavailable ([Steam]({storeUrl}))");
                    continue;
                }

                if (appDetails.Data.IsFree)
                {
                    lines.Add($"- **{item.Name}** — **Free to Play** ([Steam]({storeUrl}))");
                    continue;
                }

                if (appDetails.Data.PriceOverview is null)
                {
                    lines.Add($"- **{item.Name}** — price unavailable ([Steam]({storeUrl}))");
                    continue;
                }

                var po = appDetails.Data.PriceOverview;
                var finalPrice = po.Final / 100.0;
                var initialPrice = po.Initial / 100.0;
                var priceCurrency = po.Currency;

                if (!priceCurrency.Equals(currency, StringComparison.OrdinalIgnoreCase))
                {
                    var rate = await CurrencyHelper.GetExchangeRateAsync(priceCurrency, currency);
                    if (rate is not null)
                    {
                        finalPrice *= rate.Value;
                        initialPrice *= rate.Value;
                        priceCurrency = currency;
                    }
                }

                var priceText = po.DiscountPercent > 0
                    ? $"**{CurrencyHelper.FormatPrice(finalPrice, priceCurrency)}** (~~{CurrencyHelper.FormatPrice(initialPrice, priceCurrency)}~~ -{po.DiscountPercent}%)"
                    : $"**{CurrencyHelper.FormatPrice(finalPrice, priceCurrency)}**";

                lines.Add($"- **{item.Name}** — {priceText} ([Steam]({storeUrl}))");
            }

            return $"**Steam prices for \"{gameName}\" ({currency}):**\n{string.Join("\n", lines)}";
        }
        catch (Exception ex)
        {
            return $"Failed to look up Steam price: {ex.Message}";
        }
    }

    private static string FormatDeal(ItadDeal deal, double exchangeRate, string displayCurrency)
    {
        var price = deal.Price.Amount * exchangeRate;
        var regular = deal.Regular.Amount * exchangeRate;
        var text = deal.Cut > 0
            ? $"  - {deal.Shop.Name}: **{CurrencyHelper.FormatPrice(price, displayCurrency)}** (~~{CurrencyHelper.FormatPrice(regular, displayCurrency)}~~ -{deal.Cut}%)"
            : $"  - {deal.Shop.Name}: **{CurrencyHelper.FormatPrice(price, displayCurrency)}**";

        return deal.Url is not null ? text + $" — [buy]({deal.Url})" : text;
    }
}
