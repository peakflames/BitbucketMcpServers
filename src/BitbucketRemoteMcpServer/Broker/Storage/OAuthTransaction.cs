namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// One in-flight <c>/authorize</c> → upstream-redirect → <c>/oauth/callback</c> round trip.
/// <see cref="ConsentTokenHash"/> defends against a confused-deputy replay of the callback: it is
/// checked against a cookie set on the browser that initiated this transaction, not against
/// anything Bitbucket sends back.
/// </summary>
public sealed record OAuthTransaction(
    string TxnId,
    string ClientId,
    string ClientRedirectUri,
    string ClientState,
    string ClientCodeChallenge,
    string UpstreamCodeVerifier,
    string Scopes,
    string? Resource,
    string ConsentTokenHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
