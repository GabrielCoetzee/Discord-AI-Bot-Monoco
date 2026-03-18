using System.Text.Json.Serialization;

namespace MonocoBot.Models.Weather;

public record GeocodingResponse(
    [property: JsonPropertyName("results")] List<GeocodingResult>? Results);

public record GeocodingResult(
    [property: JsonPropertyName("latitude")] double Latitude,
    [property: JsonPropertyName("longitude")] double Longitude,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("country")] string? Country);

public record CurrentWeatherResponse(
    [property: JsonPropertyName("timezone")] string? Timezone,
    [property: JsonPropertyName("current")] CurrentWeatherData Current);

public record CurrentWeatherData(
    [property: JsonPropertyName("temperature_2m")] double Temperature,
    [property: JsonPropertyName("apparent_temperature")] double ApparentTemperature,
    [property: JsonPropertyName("relative_humidity_2m")] int RelativeHumidity,
    [property: JsonPropertyName("precipitation")] double Precipitation,
    [property: JsonPropertyName("wind_speed_10m")] double WindSpeed,
    [property: JsonPropertyName("wind_gusts_10m")] double WindGusts,
    [property: JsonPropertyName("weather_code")] int WeatherCode);

public record ForecastResponse(
    [property: JsonPropertyName("daily")] DailyForecastData Daily);

public record DailyForecastData(
    [property: JsonPropertyName("time")] List<string> Time,
    [property: JsonPropertyName("weather_code")] List<int> WeatherCode,
    [property: JsonPropertyName("temperature_2m_max")] List<double> TempMax,
    [property: JsonPropertyName("temperature_2m_min")] List<double> TempMin,
    [property: JsonPropertyName("apparent_temperature_max")] List<double> FeelsMax,
    [property: JsonPropertyName("apparent_temperature_min")] List<double> FeelsMin,
    [property: JsonPropertyName("precipitation_sum")] List<double> PrecipSum,
    [property: JsonPropertyName("precipitation_probability_max")] List<int> PrecipProbability,
    [property: JsonPropertyName("wind_speed_10m_max")] List<double> WindMax);
