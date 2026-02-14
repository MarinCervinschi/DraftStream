using Infisical.Sdk;
using Infisical.Sdk.Model;
using Microsoft.Extensions.Configuration;

namespace DraftStream.Infrastructure.Configuration;

public sealed class InfisicalConfigurationProvider : ConfigurationProvider
{
    private readonly IConfiguration _bootstrapConfig;

    public InfisicalConfigurationProvider(IConfiguration bootstrapConfig)
    {
        _bootstrapConfig = bootstrapConfig;
    }

    public override void Load()
    {
        if (!HasRequiredCredentials())
        {
            return;
        }

        try
        {
            LoadAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to load secrets from Infisical during configuration initialization", ex);
        }
    }

    private bool HasRequiredCredentials()
    {
        string? clientId = _bootstrapConfig["Infisical:ClientId"];
        string? clientSecret = _bootstrapConfig["Infisical:ClientSecret"];
        return !string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret);
    }

    private async Task LoadAsync()
    {
        string projectId = GetRequiredConfig("Infisical:ProjectId");
        string environment = GetRequiredConfig("Infisical:Environment");
        string clientId = GetRequiredConfig("Infisical:ClientId");
        string clientSecret = GetRequiredConfig("Infisical:ClientSecret");

        string siteUrl = _bootstrapConfig["Infisical:SiteUrl"] ?? "https://app.infisical.com";

        var settings = new InfisicalSdkSettingsBuilder()
            .WithHostUri(siteUrl)
            .Build();

        var client = new InfisicalClient(settings);

        await client.Auth().UniversalAuth().LoginAsync(clientId, clientSecret);

        Secret[] secrets = await client.Secrets().ListAsync(new ListSecretsOptions
        {
            ProjectId = projectId,
            EnvironmentSlug = environment,
            SecretPath = "/",
            ExpandSecretReferences = true,
            ViewSecretValue = true
        });

        foreach (Secret secret in secrets)
        {
            string configKey = secret.SecretKey.Replace("__", ConfigurationPath.KeyDelimiter);
            Data[configKey] = secret.SecretValue;
        }
    }

    private string GetRequiredConfig(string key)
    {
        string? value = _bootstrapConfig[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Infisical configuration value '{key}' is missing or empty. " +
                $"Provide it via appsettings.json or environment variables.");
        }

        return value;
    }
}
