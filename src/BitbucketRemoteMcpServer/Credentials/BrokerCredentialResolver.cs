namespace BitbucketRemoteMcpServer.Credentials;

/// <summary>
/// Active whenever <c>Broker:Enabled</c> is true. Reads the caller's own <c>jti</c> claim — set
/// by <see cref="JwtIssuer.IssueAccessToken"/>, kept as-is by <c>MapInboundClaims = false</c>
/// (see <see cref="ConfigureJwtBearerOptions"/>) — maps it to an <c>upstream_token_id</c> via
/// <see cref="JtiMappingStore"/>, and returns that caller's own Bitbucket access token. Refreshes
/// and persists it first if it has expired; never falls back to the shared credential on a
/// resolution or refresh failure, matching <c>TokenEndpoint</c>'s identical rule — a failed
/// refresh means the caller must go through a real browser consent again, not silently borrow the
/// service account's access.
/// </summary>
public sealed class BrokerCredentialResolver(
    JtiMappingStore jtiMappingStore,
    UpstreamTokenStore upstreamTokenStore,
    UpstreamOAuthClient upstreamOAuthClient,
    ILogger<BrokerCredentialResolver> logger) : IUpstreamCredentialResolver
{
    private const string NeedsReauthMessage =
        "Your Bitbucket authorization was not found or has expired. Reconnect this MCP client to re-authenticate.";

    public async Task<Result<string?>> ResolveAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default)
    {
        var jti = user?.FindFirst("jti")?.Value;
        if (string.IsNullOrEmpty(jti))
        {
            logger.LogWarning("BrokerCredentialResolver: caller's token carries no 'jti' claim; cannot resolve a per-user Bitbucket credential.");
            return Result.Fail(NeedsReauthMessage);
        }

        var upstreamTokenId = jtiMappingStore.TryGetUpstreamTokenId(jti);
        if (upstreamTokenId is null)
        {
            logger.LogWarning("BrokerCredentialResolver: no live jti_mappings row for this caller's jti.");
            return Result.Fail(NeedsReauthMessage);
        }

        var tokenSet = upstreamTokenStore.TryGet(upstreamTokenId);
        if (tokenSet is null)
        {
            logger.LogWarning("BrokerCredentialResolver: jti mapping points at upstream_token_id {UpstreamTokenId}, but no upstream_tokens row exists.", upstreamTokenId);
            return Result.Fail(NeedsReauthMessage);
        }

        var now = DateTimeOffset.UtcNow;
        if (tokenSet.AccessExpiresAt <= now)
        {
            if (string.IsNullOrEmpty(tokenSet.RefreshToken))
            {
                upstreamTokenStore.Delete(tokenSet.UpstreamTokenId);
                return Result.Fail(NeedsReauthMessage);
            }

            var refreshed = await upstreamOAuthClient.RefreshAsync(tokenSet.RefreshToken, cancellationToken);
            if (refreshed is null)
            {
                upstreamTokenStore.Delete(tokenSet.UpstreamTokenId);
                return Result.Fail(NeedsReauthMessage);
            }

            tokenSet = tokenSet with
            {
                AccessToken = refreshed.AccessToken,
                RefreshToken = refreshed.RefreshToken ?? tokenSet.RefreshToken,
                AccessExpiresAt = now.AddSeconds(refreshed.ExpiresInSeconds),
                UpdatedAt = now,
            };
            upstreamTokenStore.Upsert(tokenSet);
        }

        return Result.Ok<string?>(tokenSet.AccessToken);
    }
}
