using MonocoBot.Services;

namespace MonocoBot.Tests.Services;

public class SystemPromptProviderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _promptFile;

    public SystemPromptProviderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(_tempDir, "Resources"));
        _promptFile = Path.Combine(_tempDir, "Resources", "system-prompt.txt");
    }

    public void Dispose() => Directory.Delete(_tempDir, true);

    [Fact]
    public void GetSystemPrompt_ReplacesBotNamePlaceholder()
    {
        File.WriteAllText(_promptFile, "You are {BotName}, the warrior.");

        // We need to test the provider by pointing AppContext.BaseDirectory to our temp dir.
        // Since SystemPromptProvider uses AppContext.BaseDirectory, we write to that path instead.
        var actualPath = Path.Combine(AppContext.BaseDirectory, "Resources", "system-prompt.txt");
        var actualDir = Path.GetDirectoryName(actualPath)!;
        Directory.CreateDirectory(actualDir);
        var originalContent = File.Exists(actualPath) ? File.ReadAllText(actualPath) : null;

        try
        {
            File.WriteAllText(actualPath, "You are {BotName}, the warrior.");
            var provider = new SystemPromptProvider("Monoco");
            Assert.Contains("Monoco", provider.GetSystemPrompt());
            Assert.DoesNotContain("{BotName}", provider.GetSystemPrompt());
        }
        finally
        {
            if (originalContent is not null)
                File.WriteAllText(actualPath, originalContent);
        }
    }

    [Fact]
    public void GetSystemPrompt_ReturnsCachedValue()
    {
        var actualPath = Path.Combine(AppContext.BaseDirectory, "Resources", "system-prompt.txt");
        var actualDir = Path.GetDirectoryName(actualPath)!;
        Directory.CreateDirectory(actualDir);
        var originalContent = File.Exists(actualPath) ? File.ReadAllText(actualPath) : null;

        try
        {
            File.WriteAllText(actualPath, "Hello {BotName}");
            var provider = new SystemPromptProvider("TestBot");
            var first = provider.GetSystemPrompt();
            var second = provider.GetSystemPrompt();
            Assert.Same(first, second);
        }
        finally
        {
            if (originalContent is not null)
                File.WriteAllText(actualPath, originalContent);
        }
    }
}
