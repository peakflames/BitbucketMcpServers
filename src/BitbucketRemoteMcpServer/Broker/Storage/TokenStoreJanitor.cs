namespace BitbucketRemoteMcpServer.Broker.Storage;

/// <summary>
/// Periodic sweep of every table with a fixed, short TTL — <c>oauth_transactions</c>,
/// <c>client_codes</c>, <c>jti_mappings</c>, and <c>our_refresh_tokens</c>. Modeled on
/// PolarionMcpServers' RbacCacheJanitor: a plain timer loop, not a cron dependency.
/// <c>upstream_tokens</c> is deliberately not swept here — its lifetime tracks Bitbucket's actual
/// refresh-token lifetime, which Bitbucket does not disclose, so nothing here guesses at it.
/// </summary>
public sealed class TokenStoreJanitor(
    OAuthTransactionStore transactions,
    ClientCodeStore clientCodes,
    JtiMappingStore jtiMappings,
    OurRefreshTokenStore refreshTokens,
    ILogger<TokenStoreJanitor> logger) : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                SweepOnce();
            }
            catch (Exception ex)
            {
                // A failed sweep must never take the broker down — expired rows are inert, just
                // wasted space, until the next tick sweeps them instead.
                logger.LogError(ex, "Token store janitor sweep failed; will retry on the next tick.");
            }
        }
    }

    /// <summary>Runs one sweep synchronously. Exposed so tests can assert eviction without
    /// waiting on <see cref="SweepInterval"/>.</summary>
    public int SweepOnce()
    {
        var deleted = transactions.DeleteExpired()
                      + clientCodes.DeleteExpired()
                      + jtiMappings.DeleteExpired()
                      + refreshTokens.DeleteExpired();

        if (deleted > 0)
            logger.LogDebug("Token store janitor swept {DeletedRowCount} expired row(s).", deleted);

        return deleted;
    }
}
