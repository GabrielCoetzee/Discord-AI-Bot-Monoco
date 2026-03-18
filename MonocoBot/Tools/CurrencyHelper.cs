using System.Globalization;
using Flurl.Http;
using MonocoBot.Models.Steam;

namespace MonocoBot.Tools;

public static class CurrencyHelper
{
    private static readonly Dictionary<string, string> CurrencySymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ZAR"] = "R", ["USD"] = "$", ["EUR"] = "€", ["GBP"] = "£",
        ["CAD"] = "CA$", ["AUD"] = "A$", ["BRL"] = "R$", ["JPY"] = "¥",
        ["CNY"] = "¥", ["INR"] = "₹", ["PLN"] = "zł", ["TRY"] = "₺",
    };

    private static readonly Dictionary<string, string> CurrencyToSteamCc = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ZAR"] = "za", ["USD"] = "us", ["EUR"] = "de", ["GBP"] = "gb",
        ["CAD"] = "ca", ["AUD"] = "au", ["BRL"] = "br", ["JPY"] = "jp",
        ["CNY"] = "cn", ["INR"] = "in", ["PLN"] = "pl", ["TRY"] = "tr",
    };

    public static string FormatPrice(double amount, string currency)
    {
        var symbol = CurrencySymbols.GetValueOrDefault(currency, currency + " ");
        return $"{symbol}{amount.ToString("F2", CultureInfo.InvariantCulture)}";
    }

    public static string GetSteamCountryCode(string currency)
        => CurrencyToSteamCc.GetValueOrDefault(currency, "za");

    public static async Task<double?> GetExchangeRateAsync(string fromCurrency, string toCurrency)
    {
        if (fromCurrency.Equals(toCurrency, StringComparison.OrdinalIgnoreCase))
            return 1.0;

        try
        {
            var result = await $"https://open.er-api.com/v6/latest/{fromCurrency.ToUpperInvariant()}"
                .WithHeader("User-Agent", Constants.BotUserAgent)
                .GetJsonAsync<ExchangeRateResponse>();

            if (result.Rates is not null &&
                result.Rates.TryGetValue(toCurrency.ToUpperInvariant(), out var rate))
                return rate;
            return null;
        }
        catch
        {
            return null;
        }
    }
}
