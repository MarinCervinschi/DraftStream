using System.Text.Json;
using DraftStream.Application.Mcp;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DraftStream.Infrastructure.Notion;

public sealed class NotionSchemaProvider : ISchemaProvider
{
    private static readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(30);

    private static readonly HashSet<string> _systemManagedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "created_time", "last_edited_time", "created_by", "last_edited_by",
        "unique_id", "formula", "rollup", "relation"
    };

    private static readonly HashSet<string> _optionBasedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "select", "multi_select", "status"
    };

    private readonly IMcpToolProvider _mcpToolProvider;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NotionSchemaProvider> _logger;

    public NotionSchemaProvider(
        IMcpToolProvider mcpToolProvider,
        IMemoryCache cache,
        ILogger<NotionSchemaProvider> logger)
    {
        _mcpToolProvider = mcpToolProvider;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DatabaseSchema> GetSchemaAsync(
        string databaseId, CancellationToken cancellationToken)
    {
        string cacheKey = $"schema:{databaseId}";

        if (_cache.TryGetValue(cacheKey, out DatabaseSchema? cached))
        {
            _logger.LogDebug("Schema cache hit for database {DatabaseId}", databaseId);
            return cached!;
        }

        _logger.LogInformation("Fetching schema for database {DatabaseId}", databaseId);

        string dataSourceId = await RetrieveDataSourceIdAsync(databaseId, cancellationToken);
        IReadOnlyList<SchemaProperty> properties = await RetrievePropertiesAsync(dataSourceId, cancellationToken);

        var schema = new DatabaseSchema
        {
            DatabaseId = databaseId,
            DataSourceId = dataSourceId,
            Properties = properties
        };

        _cache.Set(cacheKey, schema, _cacheDuration);

        _logger.LogInformation(
            "Pre-fetched schema for database {DatabaseId}: {PropertyCount} properties, DataSourceId={DataSourceId}",
            databaseId, properties.Count, dataSourceId);

        return schema;
    }

    private async Task<string> RetrieveDataSourceIdAsync(
        string databaseId, CancellationToken cancellationToken)
    {
        string argsJson = JsonSerializer.Serialize(new { database_id = databaseId });

        McpToolResult result = await _mcpToolProvider.CallToolDirectAsync(
            "API-retrieve-a-database", argsJson, cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                $"API-retrieve-a-database failed for database '{databaseId}': {result.Content}");
        }

        using var doc = JsonDocument.Parse(result.Content);

        if (doc.RootElement.TryGetProperty("data_sources", out JsonElement dataSources)
            && dataSources.GetArrayLength() > 0
            && dataSources[0].TryGetProperty("id", out JsonElement idElement))
        {
            return idElement.GetString()
                   ?? throw new InvalidOperationException(
                       $"data_sources[0].id is null in API-retrieve-a-database response for database '{databaseId}'");
        }

        throw new InvalidOperationException(
            $"No data_sources found in API-retrieve-a-database response for database '{databaseId}'");
    }

    private async Task<IReadOnlyList<SchemaProperty>> RetrievePropertiesAsync(
        string dataSourceId, CancellationToken cancellationToken)
    {
        string argsJson = JsonSerializer.Serialize(new { data_source_id = dataSourceId });

        McpToolResult result = await _mcpToolProvider.CallToolDirectAsync(
            "API-retrieve-a-data-source", argsJson, cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                $"API-retrieve-a-data-source failed for data source '{dataSourceId}': {result.Content}");
        }

        using var doc = JsonDocument.Parse(result.Content);

        if (!doc.RootElement.TryGetProperty("properties", out JsonElement propertiesElement))
        {
            throw new InvalidOperationException(
                $"No 'properties' found in API-retrieve-a-data-source response for data source '{dataSourceId}'");
        }

        var properties = new List<SchemaProperty>();

        foreach (JsonProperty prop in propertiesElement.EnumerateObject())
        {
            string name = prop.Value.TryGetProperty("name", out JsonElement nameEl)
                ? nameEl.GetString() ?? prop.Name
                : prop.Name;

            string type = prop.Value.TryGetProperty("type", out JsonElement typeEl)
                ? typeEl.GetString() ?? "unknown"
                : "unknown";

            bool isSystemManaged = _systemManagedTypes.Contains(type);
            IReadOnlyList<string>? allowedValues = null;

            if (!isSystemManaged && _optionBasedTypes.Contains(type)
                                 && prop.Value.TryGetProperty(type, out JsonElement typeConfig)
                                 && typeConfig.TryGetProperty("options", out JsonElement options))
            {
                allowedValues = options.EnumerateArray()
                    .Where(o => o.TryGetProperty("name", out _))
                    .Select(o => o.GetProperty("name").GetString()!)
                    .ToList();
            }

            properties.Add(new SchemaProperty
            {
                Name = name,
                Type = type,
                IsSystemManaged = isSystemManaged,
                AllowedValues = allowedValues
            });
        }

        return properties;
    }
}
