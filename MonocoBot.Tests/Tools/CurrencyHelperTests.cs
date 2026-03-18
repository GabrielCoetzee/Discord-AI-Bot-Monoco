using Flurl.Http.Testing;
using MonocoBot.Tools;

namespace MonocoBot.Tests.Tools;

public class CurrencyHelperTests : IDisposable
{
    private readonly HttpTest _httpTest = new();

    public void Dispose() => _httpTest.Dispose();

    [Theory]
    [InlineData("ZAR", 100.0, "R100.00")]
    [InlineData("USD", 9.99, "$9.99")]
    [InlineData("EUR", 5.5, "€5.50")]
    [InlineData("GBP", 3.0, "£3.00")]
    [InlineData("XXX", 1.0, "XXX 1.00")]
    public void FormatPrice_ReturnsCorrectSymbol(string currency, double amount, string expected)
    {
        var result = CurrencyHelper.FormatPrice(amount, currency);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("ZAR", "za")]
    [InlineData("USD", "us")]
    [InlineData("EUR", "de")]
    [InlineData("GBP", "gb")]
    [InlineData("XYZ", "za")] // unknown defaults to za
    public void GetSteamCountryCode_ReturnsCorrectCode(string currency, string expected)
    {
        var result = CurrencyHelper.GetSteamCountryCode(currency);
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_SameCurrency_ReturnsOne()
    {
        var result = await CurrencyHelper.GetExchangeRateAsync("USD", "USD");
        Assert.Equal(1.0, result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ValidResponse_ReturnsRate()
    {
        _httpTest.ForCallsTo("https://open.er-api.com/*")
            .RespondWithJson(new { rates = new Dictionary<string, double> { ["ZAR"] = 18.5 } });

        var result = await CurrencyHelper.GetExchangeRateAsync("USD", "ZAR");
        Assert.Equal(18.5, result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_ApiFailure_ReturnsNull()
    {
        _httpTest.ForCallsTo("https://open.er-api.com/*")
            .RespondWith(status: 500);

        var result = await CurrencyHelper.GetExchangeRateAsync("USD", "ZAR");
        Assert.Null(result);
    }

    [Fact]
    public async Task GetExchangeRateAsync_MissingRate_ReturnsNull()
    {
        _httpTest.ForCallsTo("https://open.er-api.com/*")
            .RespondWithJson(new { rates = new Dictionary<string, double>() });

        var result = await CurrencyHelper.GetExchangeRateAsync("USD", "ZAR");
        Assert.Null(result);
    }
}
