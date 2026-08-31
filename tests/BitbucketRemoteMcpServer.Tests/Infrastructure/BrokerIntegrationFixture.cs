namespace BitbucketRemoteMcpServer.Tests.Infrastructure;

/// <summary>
/// Composes McpAuth + Broker against a fake upstream (Bitbucket) OAuth server — the harness for
/// the broker's endpoint tests. Unlike <see cref="AuthIntegrationFixture"/> (an external AS this
/// server merely validates tokens from), here this server IS the authorization server, so
/// McpAuth:Issuer is only present to satisfy McpAuthOptionsValidator; ConfigureJwtBearerOptions
/// ignores it once SigningKeyProvider is registered and uses the in-process key instead.
/// </summary>
public sealed class BrokerIntegrationFixture : IAsyncLifetime
{
    public const string ResourceUri = "https://bitbucket-mcp.example.invalid/mcp";
    public const string IssuerUri = "https://bitbucket-mcp.example.invalid";
    public const string ClientId = "test-client";
    public const string ClientRedirectUri = "http://127.0.0.1:54321/callback";

    public FakeUpstreamOAuthServer UpstreamOAuth { get; private set; } = null!;

    private BitbucketMcpServerFactory? _factory;
    private readonly List<string> _databasePaths = [];

    public BitbucketMcpServerFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    /// <summary>DCR is off by default; a couple of tests need it on, so they build a private
    /// factory rather than sharing this fixture's.</summary>
    public BitbucketMcpServerFactory BuildFactoryWithDcrEnabled() => BuildFactory(dcrEnabled: true);

    public async Task InitializeAsync()
    {
        UpstreamOAuth = await FakeUpstreamOAuthServer.StartAsync();
        _factory = BuildFactory(dcrEnabled: false);
    }

    private BitbucketMcpServerFactory BuildFactory(bool dcrEnabled)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"broker-test-{Guid.NewGuid():N}.db");
        _databasePaths.Add(databasePath);

        return new BitbucketMcpServerFactory()
            .WithEnvironment("Development")
            .With("McpAuth:Enabled", "true")
            .With("McpAuth:ResourceUri", ResourceUri)
            .With("McpAuth:Issuer", IssuerUri)
            .With("Broker:Enabled", "true")
            .With("Broker:DatabasePath", databasePath)
            .With("Broker:IssuerUri", IssuerUri)
            .With("Broker:DcrEnabled", dcrEnabled ? "true" : "false")
            .With("Broker:UpstreamAuthorizeUrl", $"{UpstreamOAuth.BaseUrl}/site/oauth2/authorize")
            .With("Broker:UpstreamTokenUrl", UpstreamOAuth.TokenUrl)
            .With("Broker:UpstreamUserInfoUrl", UpstreamOAuth.UserInfoUrl)
            .With("Broker:UpstreamClientId", "our-consumer-key")
            .With("Broker:UpstreamClientSecret", "our-consumer-secret")
            .With("Broker:StaticClients:0:ClientId", ClientId)
            .With("Broker:StaticClients:0:RedirectUris:0", ClientRedirectUri);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await UpstreamOAuth.DisposeAsync();

        SqliteConnection.ClearAllPools();
        foreach (var path in _databasePaths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
