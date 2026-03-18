namespace MonocoBot.Services;

public class SystemPromptProvider : ISystemPromptProvider
{
    private readonly string _prompt;

    public SystemPromptProvider(string botName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "system-prompt.txt");
        var template = File.ReadAllText(path);

        _prompt = template.Replace("{BotName}", botName);
    }

    public string GetSystemPrompt() => _prompt;
}
