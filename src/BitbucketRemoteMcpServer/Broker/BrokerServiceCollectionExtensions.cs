namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// Deliberate copy of the shape of Auth's AddMcpAuth and Access's AddAccess: reads
/// Broker:Enabled eagerly and returns false before registering anything — no options bind, no
/// ValidateOnStart, no storage. Disabled by default, same posture as McpAuth and Access, so OSS
/// consumers who never opt in see no behavior change.
/// </summary>
public static class BrokerServiceCollectionExtensions
{
    public static bool AddBroker(this WebApplicationBuilder builder)
    {
        var enabled = builder.Configuration.GetValue($"{BrokerOptions.SectionName}:Enabled", false);
        if (!enabled)
            return false;

        var services = builder.Services;

        services.AddOptions<BrokerOptions>()
            .Bind(builder.Configuration.GetSection(BrokerOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<BrokerOptions>, BrokerOptionsValidator>();

        services.AddSingleton<BrokerDbConnectionFactory>();
        services.AddSingleton<OAuthTransactionStore>();
        services.AddSingleton<ClientCodeStore>();
        services.AddSingleton<UpstreamTokenStore>();
        services.AddSingleton<JtiMappingStore>();
        services.AddSingleton<OurRefreshTokenStore>();
        services.AddSingleton<AppMetaStore>();
        services.AddHostedService<TokenStoreJanitor>();

        return true;
    }
}
