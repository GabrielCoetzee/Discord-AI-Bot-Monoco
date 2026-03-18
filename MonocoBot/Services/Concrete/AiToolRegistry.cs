using Microsoft.Extensions.AI;
using MonocoBot.Tools;

namespace MonocoBot.Services;

public class AiToolRegistry : IAiToolRegistry
{
    private readonly IReadOnlyList<AITool> _tools;

    public AiToolRegistry(
        PdfTools pdfTools,
        CodeRunnerTools codeRunnerTools,
        WebSearchTools webSearchTools,
        SteamTools steamTools,
        DateTimeTools dateTimeTools,
        WeatherTools weatherTools)
    {
        _tools =
        [
            AIFunctionFactory.Create(pdfTools.CreatePdf),
            AIFunctionFactory.Create(codeRunnerTools.RunCSharpCode),
            AIFunctionFactory.Create(webSearchTools.SearchWeb),
            AIFunctionFactory.Create(webSearchTools.ReadWebPage),
            AIFunctionFactory.Create(steamTools.GetLocalProfileData),
            AIFunctionFactory.Create(steamTools.LookupGameDeals),
            AIFunctionFactory.Create(steamTools.LookupSteamPrice),
            AIFunctionFactory.Create(dateTimeTools.GetCurrentDateTime),
            AIFunctionFactory.Create(dateTimeTools.ConvertTimezone),
            AIFunctionFactory.Create(weatherTools.GetCurrentWeather),
            AIFunctionFactory.Create(weatherTools.GetWeatherForecast),
        ];
    }

    public IReadOnlyList<AITool> GetTools() => _tools;
}
