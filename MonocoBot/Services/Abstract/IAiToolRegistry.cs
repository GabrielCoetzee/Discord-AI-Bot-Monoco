using Microsoft.Extensions.AI;

namespace MonocoBot.Services;

public interface IAiToolRegistry
{
    IReadOnlyList<AITool> GetTools();
}
