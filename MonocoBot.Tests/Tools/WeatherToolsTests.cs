using Flurl.Http.Testing;
using MonocoBot.Tools;

namespace MonocoBot.Tests.Tools;

public class WeatherToolsTests : IDisposable
{
    private readonly HttpTest _httpTest = new();
    private readonly WeatherTools _tools = new();

    public void Dispose() => _httpTest.Dispose();

    private static object MakeGeoResponse(string name = "Cape Town", string country = "South Africa") =>
        new
        {
            results = new[]
            {
                new { latitude = -33.93, longitude = 18.42, name, country }
            }
        };

    private static object MakeCurrentWeatherResponse() =>
        new
        {
            timezone = "Africa/Johannesburg",
            current = new
            {
                temperature_2m = 22.5,
                apparent_temperature = 21.0,
                relative_humidity_2m = 65,
                precipitation = 0.0,
                wind_speed_10m = 15.0,
                wind_gusts_10m = 25.0,
                weather_code = 1
            }
        };

    [Fact]
    public async Task GetCurrentWeather_ValidLocation_ReturnsWeatherData()
    {
        _httpTest.ForCallsTo("https://geocoding-api.open-meteo.com/*")
            .RespondWithJson(MakeGeoResponse());
        _httpTest.ForCallsTo("https://api.open-meteo.com/*")
            .RespondWithJson(MakeCurrentWeatherResponse());

        var result = await _tools.GetCurrentWeather("Cape Town");

        Assert.Contains("Cape Town", result);
        Assert.Contains("22.5", result);
        Assert.Contains("Mainly clear", result);
    }

    [Fact]
    public async Task GetCurrentWeather_UnknownLocation_ReturnsErrorMessage()
    {
        _httpTest.ForCallsTo("https://geocoding-api.open-meteo.com/*")
            .RespondWithJson(new { results = Array.Empty<object>() });

        var result = await _tools.GetCurrentWeather("XYZNotACity");

        Assert.Contains("Could not find location", result);
    }

    [Fact]
    public async Task GetWeatherForecast_ValidLocation_ReturnsForecastData()
    {
        _httpTest.ForCallsTo("https://geocoding-api.open-meteo.com/*")
            .RespondWithJson(MakeGeoResponse());
        _httpTest.ForCallsTo("https://api.open-meteo.com/*")
            .RespondWithJson(new
            {
                daily = new
                {
                    time = new[] { "2024-06-15", "2024-06-16", "2024-06-17" },
                    weather_code = new[] { 0, 1, 2 },
                    temperature_2m_max = new[] { 25.0, 24.0, 23.0 },
                    temperature_2m_min = new[] { 15.0, 14.0, 13.0 },
                    apparent_temperature_max = new[] { 24.0, 23.0, 22.0 },
                    apparent_temperature_min = new[] { 14.0, 13.0, 12.0 },
                    precipitation_sum = new[] { 0.0, 0.0, 1.5 },
                    precipitation_probability_max = new[] { 5, 10, 60 },
                    wind_speed_10m_max = new[] { 20.0, 18.0, 25.0 }
                }
            });

        var result = await _tools.GetWeatherForecast("Cape Town", 3);

        Assert.Contains("3-day forecast", result);
        Assert.Contains("2024-06-15", result);
    }

    [Fact]
    public async Task GetWeatherForecast_GeocodingFailure_ReturnsErrorMessage()
    {
        _httpTest.ForCallsTo("https://geocoding-api.open-meteo.com/*")
            .RespondWithJson(new { results = Array.Empty<object>() });

        var result = await _tools.GetWeatherForecast("XYZNotACity", 3);

        Assert.Contains("Could not find location", result);
    }
}
