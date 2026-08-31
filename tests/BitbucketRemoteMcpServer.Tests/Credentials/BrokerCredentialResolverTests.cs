namespace BitbucketRemoteMcpServer.Tests.Credentials;

/// <summary>
/// Exercises <see cref="BrokerCredentialResolver"/> directly against the real SQLite stores
/// <see cref="BrokerIntegrationFixture"/> wires up, rather than through the HTTP endpoints —
/// this is the seam between a validated caller and their own Bitbucket access
/// token, and the "two different callers get two different tokens" test here is the mechanism
/// behind the acceptance bar (two callers, same tool call, different results).
/// </summary>
public class BrokerCredentialResolverTests : IClassFixture<BrokerIntegrationFixture>
{
    private readonly BrokerIntegrationFixture _fixture;

    public BrokerCredentialResolverTests(BrokerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private IUpstreamCredentialResolver Resolver =>
        _fixture.Factory.Services.CreateScope().ServiceProvider.GetRequiredService<IUpstreamCredentialResolver>();

    private static ClaimsPrincipal PrincipalWithJti(string jti) =>
        new(new ClaimsIdentity([new Claim("jti", jti)], authenticationType: "Test"));

    private void SeedUpstreamToken(
        string upstreamTokenId, string jti, string subject, string accessToken,
        string? refreshToken = "a-refresh-token", DateTimeOffset? accessExpiresAt = null)
    {
        var now = DateTimeOffset.UtcNow;
        var upstreamTokenStore = _fixture.Factory.Services.GetRequiredService<UpstreamTokenStore>();
        var jtiMappingStore = _fixture.Factory.Services.GetRequiredService<JtiMappingStore>();

        upstreamTokenStore.Upsert(new UpstreamTokenSet(
            UpstreamTokenId: upstreamTokenId,
            Subject: subject,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            TokenType: "Bearer",
            AccessExpiresAt: accessExpiresAt ?? now.AddHours(1),
            CreatedAt: now,
            UpdatedAt: now));

        jtiMappingStore.Insert(jti, upstreamTokenId, now, now.AddMinutes(10));
    }

    [Fact]
    public async Task ResolveAsync_NoUser_FailsClosed()
    {
        var result = await Resolver.ResolveAsync(user: null);

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task ResolveAsync_JtiWithNoMapping_FailsClosed()
    {
        var result = await Resolver.ResolveAsync(PrincipalWithJti(Guid.NewGuid().ToString("N")));

        Assert.True(result.IsFailed);
    }

    [Fact]
    public async Task ResolveAsync_ValidMapping_ReturnsThatCallersOwnAccessToken()
    {
        var upstreamTokenId = Guid.NewGuid().ToString("N");
        var jti = Guid.NewGuid().ToString("N");
        SeedUpstreamToken(upstreamTokenId, jti, subject: "user-a", accessToken: "token-a");

        var result = await Resolver.ResolveAsync(PrincipalWithJti(jti));

        Assert.True(result.IsSuccess);
        Assert.Equal("token-a", result.Value);
    }

    [Fact]
    public async Task ResolveAsync_TwoDifferentCallers_ReturnDifferentAccessTokens()
    {
        var jtiA = Guid.NewGuid().ToString("N");
        var jtiB = Guid.NewGuid().ToString("N");
        SeedUpstreamToken(Guid.NewGuid().ToString("N"), jtiA, subject: "user-a", accessToken: "token-for-a");
        SeedUpstreamToken(Guid.NewGuid().ToString("N"), jtiB, subject: "user-b", accessToken: "token-for-b");

        var resultA = await Resolver.ResolveAsync(PrincipalWithJti(jtiA));
        var resultB = await Resolver.ResolveAsync(PrincipalWithJti(jtiB));

        Assert.Equal("token-for-a", resultA.Value);
        Assert.Equal("token-for-b", resultB.Value);
    }

    [Fact]
    public async Task ResolveAsync_ExpiredAccessToken_RefreshesAndPersistsTheNewToken()
    {
        var upstreamTokenId = Guid.NewGuid().ToString("N");
        var jti = Guid.NewGuid().ToString("N");
        SeedUpstreamToken(
            upstreamTokenId, jti, subject: "user-c", accessToken: "stale-token",
            accessExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        _fixture.UpstreamOAuth.NextAccessToken = "refreshed-token";
        _fixture.UpstreamOAuth.NextRefreshToken = "rotated-refresh-token";

        var result = await Resolver.ResolveAsync(PrincipalWithJti(jti));

        Assert.True(result.IsSuccess);
        Assert.Equal("refreshed-token", result.Value);

        var upstreamTokenStore = _fixture.Factory.Services.GetRequiredService<UpstreamTokenStore>();
        var persisted = upstreamTokenStore.TryGet(upstreamTokenId);
        Assert.NotNull(persisted);
        Assert.Equal("refreshed-token", persisted!.AccessToken);
        Assert.Equal("rotated-refresh-token", persisted.RefreshToken);
    }

    [Fact]
    public async Task ResolveAsync_ExpiredAccessToken_RefreshFails_DeletesTheRecordAndFailsClosed()
    {
        var upstreamTokenId = Guid.NewGuid().ToString("N");
        var jti = Guid.NewGuid().ToString("N");
        SeedUpstreamToken(
            upstreamTokenId, jti, subject: "user-d", accessToken: "stale-token",
            accessExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        _fixture.UpstreamOAuth.NextExchangeShouldFail = true;

        var result = await Resolver.ResolveAsync(PrincipalWithJti(jti));

        Assert.True(result.IsFailed);

        var upstreamTokenStore = _fixture.Factory.Services.GetRequiredService<UpstreamTokenStore>();
        Assert.Null(upstreamTokenStore.TryGet(upstreamTokenId));
    }
}
