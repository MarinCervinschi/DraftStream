using Microsoft.Extensions.AI;

namespace DraftStream.Application.Mcp;

public interface IMcpToolProvider
{
    Task<IReadOnlyList<AITool>> GetToolsAsync(CancellationToken cancellationToken);

    Task<McpToolResult> CallToolDirectAsync(string toolName, string argumentsJson, CancellationToken cancellationToken);
}
