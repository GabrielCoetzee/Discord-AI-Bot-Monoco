using System.ComponentModel;

namespace MonocoBot.Tools;

public class DateTimeTools
{
    [Description("Gets the current date and time, optionally in a specific timezone.")]
    public string GetCurrentDateTime(
        [Description("Optional IANA timezone ID (e.g., 'America/New_York', 'Europe/London', 'Asia/Tokyo'). Leave empty or null for UTC.")] string? timezoneId = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(timezoneId))
                return $"Current UTC time: {DateTime.UtcNow:dddd, yyyy-MM-dd HH:mm:ss} UTC";

            var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            var time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            return $"Current time in {tz.DisplayName}: {time:dddd, yyyy-MM-dd HH:mm:ss}";
        }
        catch (TimeZoneNotFoundException)
        {
            return $"Unknown timezone '{timezoneId}'. Use IANA format like 'America/New_York' or 'Europe/London'.";
        }
    }

    [Description("Converts a date/time value from one timezone to another.")]
    public string ConvertTimezone(
        [Description("The date/time string (e.g., '2024-06-15 14:30')")] string dateTime,
        [Description("Source timezone ID (e.g., 'America/New_York')")] string fromTimezone,
        [Description("Target timezone ID (e.g., 'Europe/London')")] string toTimezone)
    {
        try
        {
            if (!DateTime.TryParse(dateTime, out var dt))
                return $"Could not parse '{dateTime}' as a date/time. Use a format like '2024-06-15 14:30'.";

            var fromTz = TimeZoneInfo.FindSystemTimeZoneById(fromTimezone);
            var toTz = TimeZoneInfo.FindSystemTimeZoneById(toTimezone);

            var utcTime = TimeZoneInfo.ConvertTimeToUtc(dt, fromTz);
            var converted = TimeZoneInfo.ConvertTimeFromUtc(utcTime, toTz);

            return $"{dt:yyyy-MM-dd HH:mm:ss} ({fromTz.StandardName}) = {converted:yyyy-MM-dd HH:mm:ss} ({toTz.StandardName})";
        }
        catch (TimeZoneNotFoundException ex)
        {
            return $"Unknown timezone: {ex.Message}";
        }
        catch (Exception ex)
        {
            return $"Conversion failed: {ex.Message}";
        }
    }
}
