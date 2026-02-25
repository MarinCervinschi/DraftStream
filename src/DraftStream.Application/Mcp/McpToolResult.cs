namespace DraftStream.Application.Mcp;

public sealed class McpToolResult
{
    public required string Content { get; init; }

    public required bool IsError { get; init; }
}
