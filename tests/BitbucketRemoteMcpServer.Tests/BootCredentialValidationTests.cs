namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Program.BuildApp requires a shared Bitbucket credential (username/token or consumer
/// key/secret) at boot — unless Broker:Enabled is true, in which case BrokerCredentialResolver
/// resolves every caller's own credential per-request and never falls back to a shared one, so
/// none is required to start the server.
/// </summary>
public sealed class BootCredentialValidationTests
{
    [Fact]
    public void BuildApp_NoSharedCredential_BrokerDisabled_ThrowsAtBoot()
    {
        using var factory = new BitbucketMcpServerFactory()
            .With("BITBUCKET_MCP_USERNAME", null)
            .With("BITBUCKET_MCP_API_TOKEN", null);

        Assert.Throws<InvalidOperationException>(() => factory.Services);
    }

    [Fact]
    public void BuildApp_NoSharedCredential_BrokerEnabled_BootsSuccessfully()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"boot-test-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new BitbucketMcpServerFactory()
                .WithEnvironment("Development")
                .With("BITBUCKET_MCP_USERNAME", null)
                .With("BITBUCKET_MCP_API_TOKEN", null)
                .With("McpAuth:Enabled", "true")
                .With("McpAuth:ResourceUri", "https://bitbucket-mcp.example.invalid/mcp")
                .With("McpAuth:Issuer", "https://bitbucket-mcp.example.invalid")
                .With("Broker:Enabled", "true")
                .With("Broker:DatabasePath", databasePath)
                .With("Broker:IssuerUri", "https://bitbucket-mcp.example.invalid")
                .With("Broker:UpstreamAuthorizeUrl", "https://bitbucket.org/site/oauth2/authorize")
                .With("Broker:UpstreamTokenUrl", "https://bitbucket.org/site/oauth2/access_token")
                .With("Broker:UpstreamUserInfoUrl", "https://api.bitbucket.org/2.0/user")
                .With("Broker:UpstreamClientId", "our-consumer-key")
                .With("Broker:UpstreamClientSecret", "our-consumer-secret")
                .With("Broker:StaticClients:0:ClientId", "test-client")
                .With("Broker:StaticClients:0:RedirectUris:0", "http://127.0.0.1:54321/callback");

            Assert.NotNull(factory.Services);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
