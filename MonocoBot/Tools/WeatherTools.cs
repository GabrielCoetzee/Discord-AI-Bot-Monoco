using System.ComponentModel;
using System.Globalization;
using Flurl.Http;
using MonocoBot.Models.Weather;

namespace MonocoBot.Tools;

public class WeatherTools
{
    private const string GeocodingBaseUrl = "https://geocoding-api.open-meteo.com/v1";
    private const string WeatherBaseUrl = "https://api.open-meteo.com/v1";

    private static readonly Dictionary<int, string> WeatherDescriptions = new()
    {
        [0] = "Clear sky",
        [1] = "Mainly clear", [2] = "Partly cloudy", [3] = "Overcast",
        [45] = "Fog", [48] = "Depositing rime fog",
        [51] = "Light drizzle", [53] = "Moderate drizzle", [55] = "Dense drizzle",
        [56] = "Light freezing drizzle", [57] = "Dense freezing drizzle",
        [61] = "Slight rain", [63] = "Moderate rain", [65] = "Heavy rain",
        [66] = "Light freezing rain", [67] = "Heavy freezing rain",
        [71] = "Slight snow fall", [73] = "Moderate snow fall", [75] = "Heavy snow fall",
        [77] = "Snow grains",
        [80] = "Slight rain showers", [81] = "Moderate rain showers", [82] = "Violent rain showers",
        [85] = "Slight snow showers", [86] = "Heavy snow showers",
        [95] = "Thunderstorm", [96] = "Thunderstorm with slight hail", [99] = "Thunderstorm with heavy hail",
    };

    [Description("Gets the current weather conditions for a location. " +
        "Returns temperature, humidity, wind, and conditions.")]
    public async Task<string> GetCurrentWeather(
        [Description("The city or location name (e.g. 'Cape Town', 'London', 'New York')")] string location)
    {
        try
        {
            var geo = await GeocodeLocationAsync(location);
            if (geo is null)
                return $"Could not find location '{location}'. Try a different city name.";

            var result = await $"{WeatherBaseUrl}/forecast"
                .WithHeader("User-Agent", Constants.BotUserAgent)
                .SetQueryParams(new
                {
                    latitude = geo.Latitude,
                    longitude = geo.Longitude,
                    current = "temperature_2m,relative_humidity_2m,apparent_temperature,precipitation,weather_code,wind_speed_10m,wind_gusts_10m",
                    timezone = "auto"
                })
                .GetJsonAsync<CurrentWeatherResponse>();

            var c = result.Current;
            var condition = WeatherDescriptions.GetValueOrDefault(c.WeatherCode, "Unknown");

            var ic = CultureInfo.InvariantCulture;
            return $"""
                **Current weather in {geo.Name}, {geo.Country}:**
                - **Conditions:** {condition}
                - **Temperature:** {c.Temperature.ToString(ic)}°C (feels like {c.ApparentTemperature.ToString(ic)}°C)
                - **Humidity:** {c.RelativeHumidity}%
                - **Precipitation:** {c.Precipitation.ToString(ic)} mm
                - **Wind:** {c.WindSpeed.ToString(ic)} km/h (gusts {c.WindGusts.ToString(ic)} km/h)
                - **Timezone:** {result.Timezone ?? "UTC"}
                """;
        }
        catch (Exception ex)
        {
            return $"Failed to get weather: {ex.Message}";
        }
    }

    [Description("Gets a multi-day weather forecast for a location. " +
        "Returns daily high/low temperatures, conditions, precipitation chance, and wind.")]
    public async Task<string> GetWeatherForecast(
        [Description("The city or location name (e.g. 'Cape Town', 'London', 'New York')")] string location,
        [Description("Number of forecast days (1-7, default: 3)")] int days = 3)
    {
        try
        {
            days = Math.Clamp(days, 1, 7);

            var geo = await GeocodeLocationAsync(location);
            if (geo is null)
                return $"Could not find location '{location}'. Try a different city name.";

            var result = await $"{WeatherBaseUrl}/forecast"
                .WithHeader("User-Agent", Constants.BotUserAgent)
                .SetQueryParams(new
                {
                    latitude = geo.Latitude,
                    longitude = geo.Longitude,
                    daily = "weather_code,temperature_2m_max,temperature_2m_min,apparent_temperature_max,apparent_temperature_min,precipitation_sum,precipitation_probability_max,wind_speed_10m_max",
                    timezone = "auto",
                    forecast_days = days
                })
                .GetJsonAsync<ForecastResponse>();

            var d = result.Daily;
            var ic = CultureInfo.InvariantCulture;
            var lines = Enumerable.Range(0, Math.Min(d.Time.Count, days)).Select(i =>
            {
                var condition = WeatherDescriptions.GetValueOrDefault(d.WeatherCode[i], "Unknown");
                return $"**{d.Time[i]}** — {condition}\n" +
                    $"  High: {d.TempMax[i].ToString(ic)}°C (feels {d.FeelsMax[i].ToString(ic)}°C) · Low: {d.TempMin[i].ToString(ic)}°C (feels {d.FeelsMin[i].ToString(ic)}°C)\n" +
                    $"  Precip: {d.PrecipSum[i].ToString(ic)} mm ({d.PrecipProbability[i]}% chance) · Wind: up to {d.WindMax[i].ToString(ic)} km/h";
            });

            return $"**{days}-day forecast for {geo.Name}, {geo.Country}:**\n{string.Join("\n", lines)}";
        }
        catch (Exception ex)
        {
            return $"Failed to get forecast: {ex.Message}";
        }
    }

    private static async Task<GeocodingResult?> GeocodeLocationAsync(string location)
    {
        var response = await $"{GeocodingBaseUrl}/search"
            .WithHeader("User-Agent", Constants.BotUserAgent)
            .SetQueryParams(new { name = location, count = 1, language = "en" })
            .GetJsonAsync<GeocodingResponse>();

        return response.Results?.FirstOrDefault();
    }
}
