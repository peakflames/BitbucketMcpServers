namespace StubAuthorizationServer;

/// <summary>Per-instance mutable stub configuration — registered as a singleton in the stub's own
/// DI container, so each StubAuthorizationServerApp instance (one per test) has its own
/// independent state.</summary>
public sealed class StubAuthorizationServerState
{
    public string DefaultSubject { get; set; } = "stub-subject";

    public string DefaultAudience { get; set; } = "https://bitbucket-mcp.example.invalid/mcp";

    public string DefaultScope { get; set; } = "bitbucket:read";

    public int DefaultLifetimeSeconds { get; set; } = 300;

    /// <summary>Fault injection: sign the access token with a throwaway key instead of
    /// SigningKey.Rsa, simulating a token signed by a key the resource server doesn't trust.</summary>
    public bool SignWithWrongKey { get; set; }
}
