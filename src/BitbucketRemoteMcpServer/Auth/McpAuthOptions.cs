namespace BitbucketRemoteMcpServer.Auth;

// Deliberately `sealed class`, never `record` — a record's generated ToString() prints every
// property, so one `Log.Debug("{@Options}", o)` would dump config values that shouldn't be logged.
// This is the cheapest structural defense for the "no secret ever logged" rule.

public sealed class McpAuthOptions
{
    public const string SectionName = "McpAuth";

    public bool Enabled { get; set; }

    /// <summary>The authorization server's issuer URI. JwtBearer fetches its OIDC discovery
    /// document and JWKS from this Authority. This can be an external OIDC-compliant AS, or — when
    /// `Broker:Enabled` is true — this same server acting as its own AS (see
    /// `Broker/BrokerServiceCollectionExtensions.cs`), which trusts its in-process signing key
    /// directly rather than fetching discovery from itself over HTTP.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Explicit override for the discovery document location. Optional escape hatch for
    /// an external AS whose discovery document does not live at the default
    /// `{Issuer}/.well-known/openid-configuration` location .NET's Authority handling assumes.
    /// Not used when `Broker:Enabled` is true.</summary>
    public string? MetadataAddress { get; set; }

    /// <summary>This server's own resource identifier — both the JWT audience and the RFC 9728
    /// protected-resource-metadata `resource` value, e.g.
    /// https://bitbucket-mcp.example.invalid/mcp.</summary>
    public string ResourceUri { get; set; } = string.Empty;

    public List<string> ScopesSupported { get; set; } = [OAuthScopes.Read];

    public int ClockSkewSeconds { get; set; } = 30;
}
