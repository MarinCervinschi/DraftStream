namespace DraftStream.Application.Mcp;

public sealed class DatabaseSchema
{
    public required string DatabaseId { get; init; }
    public required string DataSourceId { get; init; }
    public required IReadOnlyList<SchemaProperty> Properties { get; init; }
}

public sealed class SchemaProperty
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public IReadOnlyList<string>? AllowedValues { get; init; }
    public bool IsSystemManaged { get; init; }
}
