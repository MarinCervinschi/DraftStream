using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;

namespace DraftStream.Application.Mcp;

/// <summary>
/// Wraps an <see cref="AIFunction"/> with <see cref="IMemoryCache"/>-based caching.
/// Suited for read-only MCP tools where repeated calls with the same arguments
/// return identical results (e.g., schema retrieval).
/// </summary>
public sealed class CachingAiFunction : DelegatingAIFunction
{
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;

    public CachingAiFunction(AIFunction innerFunction, IMemoryCache cache, TimeSpan cacheDuration)
        : base(innerFunction)
    {
        _cache = cache;
        _cacheDuration = cacheDuration;
    }

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        string cacheKey = BuildCacheKey(arguments);

        if (_cache.TryGetValue(cacheKey, out object? cached))
            return cached;

        object? result = await base.InvokeCoreAsync(arguments, cancellationToken);

        _cache.Set(cacheKey, result, _cacheDuration);

        return result;
    }

    private string BuildCacheKey(AIFunctionArguments arguments)
    {
        var sorted = arguments
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        string serializedArgs = JsonSerializer.Serialize(sorted);

        return $"tool:{Name}:{serializedArgs}";
    }
}
