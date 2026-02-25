using DraftStream.Application.Llm;

namespace DraftStream.Application.Mcp;

public interface IMcpToolClient
{
    Task<IReadOnlyList<LlmToolDefinition>> GetToolDefinitionsAsync(CancellationToken cancellationToken);

    Task<McpToolResult> CallToolAsync(string toolName, string argumentsJson, CancellationToken cancellationToken);
}
