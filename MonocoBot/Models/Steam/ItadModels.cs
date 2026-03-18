using System.Text.Json.Serialization;

namespace MonocoBot.Models.Steam;

public record ItadSearchResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("slug")] string? Slug);

public record ItadPriceResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("deals")] List<ItadDeal>? Deals);

public record ItadDeal(
    [property: JsonPropertyName("shop")] ItadShop Shop,
    [property: JsonPropertyName("price")] ItadAmount Price,
    [property: JsonPropertyName("regular")] ItadAmount Regular,
    [property: JsonPropertyName("cut")] int Cut,
    [property: JsonPropertyName("url")] string? Url);

public record ItadShop(
    [property: JsonPropertyName("name")] string Name);

public record ItadAmount(
    [property: JsonPropertyName("amount")] double Amount);
