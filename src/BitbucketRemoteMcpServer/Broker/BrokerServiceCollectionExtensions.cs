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
        services.AddSingleton<RegisteredClientStore>();
        services.AddHostedService<TokenStoreJanitor>();

        // The authorization-server pieces layered on top of the storage registered above: this
        // server's own signing key, PKCE/redirect-uri/client-lookup helpers, the upstream
        // (Bitbucket) OAuth leg, and JWT issuance.
        services.AddSingleton<SigningKeyProvider>();
        services.AddSingleton<ClientLookup>();
        services.AddSingleton<JwtIssuer>();
        services.AddHttpClient(UpstreamOAuthClient.HttpClientName);
        services.AddSingleton<UpstreamOAuthClient>();

        // Overrides Program.cs's default SharedCredentialResolver registration (registered
        // first, so this later registration wins) — per-user Bitbucket credentials only make
        // sense once the broker actually has upstream tokens to resolve them from.
        services.AddScoped<Credentials.IUpstreamCredentialResolver, Credentials.BrokerCredentialResolver>();

        // Belt-and-suspenders: every broker endpoint passes its response's JsonTypeInfo to
        // Results.Json(...) explicitly (see BrokerResponseModels.cs for why — reflection-based
        // JSON is unavailable at runtime under PublishTrimmed), but registering the context here
        // too means a future endpoint that forgets still resolves instead of throwing.
        services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, Endpoints.BrokerJsonContext.Default));

        return true;
    }
}
