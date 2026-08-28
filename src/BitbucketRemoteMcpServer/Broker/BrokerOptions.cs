namespace BitbucketRemoteMcpServer.Broker;

// Deliberately `sealed class`, never `record` — same rationale as `McpAuthOptions` and
// `AccessOptions`: a record's generated ToString() prints every property, which would print
// UpstreamClientSecret in a stray `Log.Debug("{@Options}", o)`.

/// <summary>
/// Configuration for the token broker: storage location, this server's own identity as an
/// authorization server, and the upstream Bitbucket OAuth consumer it delegates to. TTL policy
/// lives here (not in the storage layer) because stores take an explicit expiry from their
/// caller rather than reading one out of options themselves.
/// </summary>
public sealed class BrokerOptions
{
    public const string SectionName = "Broker";

    public bool Enabled { get; set; }

    /// <summary>
    /// Path to the SQLite database file. Relative paths resolve against the current working
    /// directory (the deployed container's mounted volume in production). If the containing
    /// directory does not exist and cannot be created, <see cref="Storage.BrokerDbConnectionFactory"/>
    /// falls back to a temp-directory path and logs a loud warning rather than crash-looping —
    /// data does not survive a restart in that fallback, which is the point of the warning.
    /// </summary>
    public string DatabasePath { get; set; } = "data/broker.db";

    /// <summary>
    /// This server's own issuer identity, e.g. https://bitbucket-mcp.example.invalid. No path, no
    /// trailing slash — /authorize, /oauth/callback, /token etc. are mounted at its root. Used as
    /// the "iss" claim on every JWT this server issues, and as the sole entry in the resource
    /// metadata's AuthorizationServers list, since with the broker enabled this server IS the
    /// authorization server, not just the resource server.
    /// </summary>
    public string IssuerUri { get; set; } = string.Empty;

    /// <summary>Bitbucket's OAuth consumer authorize endpoint. Overridable only so tests can
    /// point it at a fake; production should leave the default.</summary>
    public string UpstreamAuthorizeUrl { get; set; } = "https://bitbucket.org/site/oauth2/authorize";

    /// <summary>Bitbucket's OAuth consumer token endpoint. Overridable only so tests can point it
    /// at a fake; production should leave the default. Deliberately called directly with
    /// HttpClient rather than through SharpBucket's OAuth2TokenProvider, whose token URL is a
    /// private hardcoded constant — not swappable without another SharpBucket fork change.</summary>
    public string UpstreamTokenUrl { get; set; } = "https://bitbucket.org/site/oauth2/access_token";

    /// <summary>Bitbucket's user-identity endpoint, called once per upstream token exchange to
    /// resolve which Bitbucket account the caller actually is — the "two different humans get
    /// different results" requirement needs a real per-user subject, not a placeholder.</summary>
    public string UpstreamUserInfoUrl { get; set; } = "https://api.bitbucket.org/2.0/user";

    /// <summary>The Bitbucket OAuth consumer's key/secret for your workspace.</summary>
    public string UpstreamClientId { get; set; } = string.Empty;

    public string UpstreamClientSecret { get; set; } = string.Empty;

    /// <summary>Scopes requested from Bitbucket on the upstream leg. Read-only by design. Never
    /// includes MCP's own "bitbucket:read" scope name; these are Bitbucket's own scope vocabulary
    /// (account, repository, pullrequest, ...).</summary>
    public List<string> UpstreamScopes { get; set; } = ["account", "repository", "pullrequest"];

    /// <summary>Advertises POST /register and accepts it when true. False by default: Claude
    /// Code does not need DCR when its clientId is pre-configured (see StaticClients), and a
    /// disabled-by-default registration endpoint is one less piece of unauthenticated attack
    /// surface until it is actually needed.</summary>
    public bool DcrEnabled { get; set; }

    /// <summary>Pre-registered MCP clients, for the window before/instead of DCR. Public clients
    /// only (PKCE is the confidentiality mechanism, not a client secret) — DCR-created clients
    /// that do carry a secret live in the `registered_clients` table instead.</summary>
    public List<StaticClient> StaticClients { get; set; } = [];

    public int TransactionLifetimeMinutes { get; set; } = 15;

    public int ClientCodeLifetimeMinutes { get; set; } = 5;

    /// <summary>Lifetime of the JWTs this server issues to MCP clients.</summary>
    public int IssuedAccessTokenLifetimeMinutes { get; set; } = 60;

    /// <summary>Lifetime of the refresh token this server issues to MCP clients — deliberately
    /// its own policy, independent of Bitbucket's upstream refresh-token lifetime, which Bitbucket
    /// does not return and so cannot be measured directly (the upstream access-token lifetime is
    /// 7200 seconds / 2 hours, confirmed against a live broker database).</summary>
    public int IssuedRefreshTokenLifetimeDays { get; set; } = 30;
}

/// <summary>A statically pre-registered public MCP client — the non-DCR equivalent of a
/// `registered_clients` row. No secret: matched only by client_id and validated against
/// RedirectUris by <see cref="RedirectUriValidator"/>.</summary>
public sealed class StaticClient
{
    public string ClientId { get; set; } = string.Empty;

    public List<string> RedirectUris { get; set; } = [];
}
