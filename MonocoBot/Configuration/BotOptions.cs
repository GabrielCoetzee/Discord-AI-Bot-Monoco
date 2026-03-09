namespace MonocoBot.Configuration;

public class BotOptions
{
    public string Name { get; set; } = "Monoco";
    public string DiscordToken { get; set; } = "";
    public string AiModel { get; set; } = "gpt-4o";
    public string AiApiKey { get; set; } = "";
    public string AiEndpoint { get; set; } = "";
    public string SteamApiKey { get; set; } = "";
    public string OwnerSteamId { get; set; } = "";
    public int MaxConversationHistory { get; set; } = 50;
}
