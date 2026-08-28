namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// The actual Bitbucket OAuth2 access/refresh token pair for one subject. Stored in plaintext by
/// deliberate decision (D6 in the research doc): these must be replayed verbatim to Bitbucket, so
/// hashing them would make them useless, and encryption is a volume-level control rather than an
/// application-level one here.
/// </summary>
public sealed record UpstreamTokenSet(
    string UpstreamTokenId,
    string Subject,
    string AccessToken,
    string? RefreshToken,
    string TokenType,
    DateTimeOffset AccessExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
