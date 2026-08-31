namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// The actual Bitbucket OAuth2 access/refresh token pair for one subject. Stored in plaintext by
/// deliberate decision: these must be replayed verbatim to Bitbucket, so
/// hashing them would make them useless — encryption of the database file itself is a
/// volume-level concern, not something this application layer does.
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
