using Microsoft.Extensions.Configuration;

namespace DraftStream.Infrastructure.Configuration;

public sealed class InfisicalConfigurationSource : IConfigurationSource
{
    private readonly IConfiguration _bootstrapConfig;

    public InfisicalConfigurationSource(IConfiguration bootstrapConfig)
    {
        _bootstrapConfig = bootstrapConfig;
    }

    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new InfisicalConfigurationProvider(_bootstrapConfig);
    }
}
