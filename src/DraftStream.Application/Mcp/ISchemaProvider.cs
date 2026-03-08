namespace DraftStream.Application.Mcp;

public interface ISchemaProvider
{
    Task<DatabaseSchema> GetSchemaAsync(string databaseId, CancellationToken cancellationToken);
}
