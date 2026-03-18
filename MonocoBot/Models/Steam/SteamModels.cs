using System.Text.Json.Serialization;

namespace MonocoBot.Models.Steam;

public record SteamSearchResponse(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("items")] List<SteamSearchItem> Items);

public record SteamSearchItem(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

public record SteamAppDetailsResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] SteamAppData? Data);

public record SteamAppData(
    [property: JsonPropertyName("is_free")] bool IsFree,
    [property: JsonPropertyName("price_overview")] SteamPriceOverview? PriceOverview);

public record SteamPriceOverview(
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("final")] int Final,
    [property: JsonPropertyName("initial")] int Initial,
    [property: JsonPropertyName("discount_percent")] int DiscountPercent);

public record ExchangeRateResponse(
    [property: JsonPropertyName("rates")] Dictionary<string, double>? Rates);
