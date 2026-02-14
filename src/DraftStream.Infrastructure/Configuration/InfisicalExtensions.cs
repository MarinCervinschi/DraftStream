using Microsoft.Extensions.Configuration;

namespace DraftStream.Infrastructure.Configuration;

public static class InfisicalExtensions
{
    public static IConfigurationBuilder AddDraftStreamInfisical(
        this IConfigurationBuilder builder,
        IConfiguration bootstrapConfig)
    {
        builder.Add(new InfisicalConfigurationSource(bootstrapConfig));
        return builder;
    }
}
