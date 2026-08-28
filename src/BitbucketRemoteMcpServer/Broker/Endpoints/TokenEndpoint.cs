namespace BitbucketRemoteMcpServer.Broker.Endpoints;

/// <summary>
/// <c>POST /token</c> — the two grant types this broker issues: exchanging a one-time client
/// code for our own JWT (verifying the client's PKCE code_verifier against the code_challenge
/// captured at <c>/authorize</c>), and rotating that JWT via our own refresh token. Bitbucket is
/// never involved in the former; it is only re-contacted by the latter if the stored Bitbucket
/// access token has actually expired.
/// </summary>
public static class TokenEndpoint
{
    public static async Task<IResult> HandleAsync(
        HttpRequest request,
        IOptions<BrokerOptions> brokerOptions,
        IOptions<McpAuthOptions> mcpAuthOptions,
        ClientCodeStore clientCodeStore,
        UpstreamTokenStore upstreamTokenStore,
        JtiMappingStore jtiMappingStore,
        OurRefreshTokenStore refreshTokenStore,
        UpstreamOAuthClient upstreamOAuthClient,
        JwtIssuer jwtIssuer,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        var form = await request.ReadFormAsync(cancellationToken);
        var grantType = form["grant_type"].ToString();

        return grantType switch
        {
            "authorization_code" => HandleAuthorizationCode(
                form, brokerOptions, mcpAuthOptions, clientCodeStore, upstreamTokenStore, jtiMappingStore,
                refreshTokenStore, jwtIssuer),
            "refresh_token" => await HandleRefreshTokenAsync(
                form, brokerOptions, mcpAuthOptions, upstreamTokenStore, jtiMappingStore, refreshTokenStore,
                upstreamOAuthClient, jwtIssuer, cancellationToken),
            _ => Results.BadRequest(new { error = "unsupported_grant_type" }),
        };
    }

    private static IResult HandleAuthorizationCode(
        IFormCollection form,
        IOptions<BrokerOptions> brokerOptions,
        IOptions<McpAuthOptions> mcpAuthOptions,
        ClientCodeStore clientCodeStore,
        UpstreamTokenStore upstreamTokenStore,
        JtiMappingStore jtiMappingStore,
        OurRefreshTokenStore refreshTokenStore,
        JwtIssuer jwtIssuer)
    {
        var code = form["code"].ToString();
        var redirectUri = form["redirect_uri"].ToString();
        var clientId = form["client_id"].ToString();
        var codeVerifier = form["code_verifier"].ToString();

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(codeVerifier))
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        // Consuming the code happens before any further validation: whether the code
        // subsequently fails a check or not, it must never be usable twice.
        var record = clientCodeStore.TryConsume(code);
        if (record is null)
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        if (!string.IsNullOrEmpty(clientId) && !string.Equals(clientId, record.ClientId, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        if (!string.IsNullOrEmpty(redirectUri) && !string.Equals(redirectUri, record.ClientRedirectUri, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        if (!PkceHelper.VerifyS256(codeVerifier, record.ClientCodeChallenge))
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        var tokenSet = upstreamTokenStore.TryGet(record.UpstreamTokenId);
        if (tokenSet is null)
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        var response = IssueTokenResponse(
            tokenSet, record.ClientId, brokerOptions, mcpAuthOptions, jtiMappingStore, refreshTokenStore, jwtIssuer);
        return Results.Json(response);
    }

    private static async Task<IResult> HandleRefreshTokenAsync(
        IFormCollection form,
        IOptions<BrokerOptions> brokerOptions,
        IOptions<McpAuthOptions> mcpAuthOptions,
        UpstreamTokenStore upstreamTokenStore,
        JtiMappingStore jtiMappingStore,
        OurRefreshTokenStore refreshTokenStore,
        UpstreamOAuthClient upstreamOAuthClient,
        JwtIssuer jwtIssuer,
        CancellationToken cancellationToken)
    {
        var refreshToken = form["refresh_token"].ToString();
        var clientId = form["client_id"].ToString();

        if (string.IsNullOrEmpty(refreshToken))
        {
            return Results.BadRequest(new { error = "invalid_request" });
        }

        var found = refreshTokenStore.TryGet(refreshToken);
        if (found is null)
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        if (!string.IsNullOrEmpty(clientId) && !string.Equals(clientId, found.Value.ClientId, StringComparison.Ordinal))
        {
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        var tokenSet = upstreamTokenStore.TryGet(found.Value.UpstreamTokenId);
        if (tokenSet is null)
        {
            refreshTokenStore.Delete(refreshToken);
            return Results.BadRequest(new { error = "invalid_grant" });
        }

        var now = DateTimeOffset.UtcNow;
        if (tokenSet.AccessExpiresAt <= now)
        {
            if (string.IsNullOrEmpty(tokenSet.RefreshToken))
            {
                refreshTokenStore.Delete(refreshToken);
                upstreamTokenStore.Delete(tokenSet.UpstreamTokenId);
                return Results.BadRequest(new { error = "invalid_grant" });
            }

            var refreshed = await upstreamOAuthClient.RefreshAsync(tokenSet.RefreshToken, cancellationToken);
            if (refreshed is null)
            {
                // Never fall back to a shared credential here — a failed upstream refresh means
                // the caller must go through a real browser consent again.
                refreshTokenStore.Delete(refreshToken);
                upstreamTokenStore.Delete(tokenSet.UpstreamTokenId);
                return Results.BadRequest(new { error = "invalid_grant" });
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

        // Rotate on every use, whether or not the upstream token needed refreshing.
        refreshTokenStore.Delete(refreshToken);

        var response = IssueTokenResponse(
            tokenSet, found.Value.ClientId, brokerOptions, mcpAuthOptions, jtiMappingStore, refreshTokenStore, jwtIssuer);
        return Results.Json(response);
    }

    private static object IssueTokenResponse(
        UpstreamTokenSet tokenSet,
        string clientId,
        IOptions<BrokerOptions> brokerOptions,
        IOptions<McpAuthOptions> mcpAuthOptions,
        JtiMappingStore jtiMappingStore,
        OurRefreshTokenStore refreshTokenStore,
        JwtIssuer jwtIssuer)
    {
        var broker = brokerOptions.Value;
        var mcpAuth = mcpAuthOptions.Value;
        var now = DateTimeOffset.UtcNow;

        var jti = Guid.NewGuid().ToString("N");
        jtiMappingStore.Insert(
            jti, tokenSet.UpstreamTokenId, now, now.AddMinutes(broker.IssuedAccessTokenLifetimeMinutes));

        var newRefreshToken = PkceHelper.GenerateOpaqueSecret();
        refreshTokenStore.Insert(
            newRefreshToken, tokenSet.UpstreamTokenId, clientId, now, now.AddDays(broker.IssuedRefreshTokenLifetimeDays));

        var scope = string.Join(' ', mcpAuth.ScopesSupported);
        var accessToken = jwtIssuer.IssueAccessToken(tokenSet.Subject, jti, scope, mcpAuth.ResourceUri, now);

        return new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = broker.IssuedAccessTokenLifetimeMinutes * 60,
            refresh_token = newRefreshToken,
            scope,
        };
    }
}
