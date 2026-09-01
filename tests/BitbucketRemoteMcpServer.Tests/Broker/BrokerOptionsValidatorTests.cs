namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Broker:Enabled without McpAuth:Enabled leaves /mcp unauthenticated, so no caller ever presents
/// a token for BrokerCredentialResolver to resolve — every tool call would fail with no startup
/// signal. BrokerOptionsValidator rejects that combination at boot instead.
/// </summary>
public sealed class BrokerOptionsValidatorTests
{
    [Fact]
    public void BuildApp_BrokerEnabled_McpAuthDisabled_ThrowsAtBoot()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"broker-mcpauth-coupling-{Guid.NewGuid():N}.db");
        try
        {
            using var factory = new BitbucketMcpServerFactory()
                .WithEnvironment("Development")
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

            Assert.Throws<OptionsValidationException>(() => factory.Services);
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
