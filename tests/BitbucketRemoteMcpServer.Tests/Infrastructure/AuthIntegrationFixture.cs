namespace BitbucketRemoteMcpServer.Tests.Infrastructure;

/// <summary>Composes the stub external AS with an McpAuth-enabled resource server pointed at
/// it — the harness shared by the challenge/discovery and token-validation test classes.</summary>
public sealed class AuthIntegrationFixture : IAsyncLifetime
{
    public const string ResourceUri = "https://bitbucket-mcp.example.invalid/mcp";

    public StubAuthorizationServerFixture StubAs { get; } = new();

    private BitbucketMcpServerFactory? _factory;

    public BitbucketMcpServerFactory Factory => _factory ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await StubAs.InitializeAsync();
        StubAs.State.DefaultAudience = ResourceUri;

        _factory = new BitbucketMcpServerFactory()
            .WithEnvironment("Development")
            .With("McpAuth:Enabled", "true")
            .With("McpAuth:Issuer", StubAs.IssuerUrl)
            .With("McpAuth:ResourceUri", ResourceUri);
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await StubAs.DisposeAsync();
    }
}
