namespace MonocoBot.Tools;

public static class ToolOutput
{
    private static readonly AsyncLocal<List<string>> _files = new();

    public static List<string> PendingFiles
    {
        get => _files.Value ??= [];
        set => _files.Value = value;
    }

    public static void AddFile(string filePath) => PendingFiles.Add(filePath);

    public static void Reset() => _files.Value = [];
}
