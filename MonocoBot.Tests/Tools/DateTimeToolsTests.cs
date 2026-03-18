using MonocoBot.Tools;

namespace MonocoBot.Tests.Tools;

public class DateTimeToolsTests
{
    private readonly DateTimeTools _tools = new();

    [Fact]
    public void GetCurrentDateTime_NullTimezone_ReturnsUtc()
    {
        var result = _tools.GetCurrentDateTime(null);
        Assert.Contains("UTC", result);
    }

    [Fact]
    public void GetCurrentDateTime_EmptyTimezone_ReturnsUtc()
    {
        var result = _tools.GetCurrentDateTime("");
        Assert.Contains("UTC", result);
    }

    [Fact]
    public void GetCurrentDateTime_ValidTimezone_ReturnsFormattedTime()
    {
        var result = _tools.GetCurrentDateTime("Europe/London");
        // Output uses tz.DisplayName (e.g. "(UTC+00:00) Dublin, Edinburgh...")
        Assert.Contains("Current time in", result);
        Assert.DoesNotContain("Unknown timezone", result);
    }

    [Fact]
    public void GetCurrentDateTime_InvalidTimezone_ReturnsErrorMessage()
    {
        var result = _tools.GetCurrentDateTime("Not/ATimezone");
        Assert.Contains("Unknown timezone", result);
    }

    [Fact]
    public void ConvertTimezone_ValidConversion_ReturnsResult()
    {
        var result = _tools.ConvertTimezone("2024-06-15 12:00", "America/New_York", "Europe/London");
        Assert.Contains("2024-06-15", result);
        Assert.DoesNotContain("failed", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConvertTimezone_InvalidDateTime_ReturnsError()
    {
        var result = _tools.ConvertTimezone("not-a-date", "America/New_York", "Europe/London");
        Assert.Contains("Could not parse", result);
    }

    [Fact]
    public void ConvertTimezone_InvalidTimezone_ReturnsError()
    {
        var result = _tools.ConvertTimezone("2024-06-15 12:00", "Invalid/Zone", "Europe/London");
        Assert.Contains("Unknown timezone", result);
    }
}
