namespace BitbucketRemoteMcpServer.Broker.Endpoints;

/// <summary>
/// <c>GET /oauth/callback</c> — Bitbucket redirects here with our own <c>txn_id</c> echoed back
/// as <c>state</c>. Checks the consent-binding cookie before doing anything else (confused-deputy
/// defense), exchanges the code server-side with this server's own upstream PKCE verifier,
/// resolves which Bitbucket account this is, and hands the browser back a one-time client code
/// scoped to the original client's redirect_uri and state.
/// </summary>
public static class CallbackEndpoint
{
    public static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        string? code,
        string? state,
        string? error,
        IOptions<BrokerOptions> brokerOptions,
        OAuthTransactionStore transactionStore,
        UpstreamTokenStore upstreamTokenStore,
        ClientCodeStore clientCodeStore,
        UpstreamOAuthClient upstreamOAuthClient,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            return BadRequest("invalid_request", "Missing state.");
        }

        var transaction = transactionStore.TryGet(state);
        if (transaction is null)
        {
            return BadRequest("invalid_request", "Unknown or expired authorization attempt.");
        }

        if (!ConsentCookie.Validate(httpContext, transaction.TxnId, transaction.ConsentTokenHash))
        {
            transactionStore.Delete(transaction.TxnId);
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        ConsentCookie.Clear(httpContext);

        if (!string.IsNullOrWhiteSpace(error))
        {
            transactionStore.Delete(transaction.TxnId);
            return RedirectWithError(transaction.ClientRedirectUri, transaction.ClientState, error);
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            transactionStore.Delete(transaction.TxnId);
            return RedirectWithError(transaction.ClientRedirectUri, transaction.ClientState, "invalid_request");
        }

        var broker = brokerOptions.Value;
        var callbackUrl = $"{broker.IssuerUri}/oauth/callback";
        var exchanged = await upstreamOAuthClient.ExchangeAuthorizationCodeAsync(
            code, callbackUrl, transaction.UpstreamCodeVerifier, cancellationToken);

        if (exchanged is null)
        {
            transactionStore.Delete(transaction.TxnId);
            logger.LogWarning("Upstream authorization_code exchange failed for txn {TxnId}.", transaction.TxnId);
            return RedirectWithError(transaction.ClientRedirectUri, transaction.ClientState, "server_error");
        }

        var subject = await upstreamOAuthClient.GetSubjectAsync(exchanged.AccessToken, cancellationToken);
        if (subject is null)
        {
            transactionStore.Delete(transaction.TxnId);
            logger.LogWarning("Upstream user-info lookup failed for txn {TxnId}.", transaction.TxnId);
            return RedirectWithError(transaction.ClientRedirectUri, transaction.ClientState, "server_error");
        }

        var now = DateTimeOffset.UtcNow;
        var upstreamTokenId = Guid.NewGuid().ToString("N");
        upstreamTokenStore.Upsert(new UpstreamTokenSet(
            UpstreamTokenId: upstreamTokenId,
            Subject: subject,
            AccessToken: exchanged.AccessToken,
            RefreshToken: exchanged.RefreshToken,
            TokenType: exchanged.TokenType,
            AccessExpiresAt: now.AddSeconds(exchanged.ExpiresInSeconds),
            CreatedAt: now,
            UpdatedAt: now));

        var ourCode = PkceHelper.GenerateOpaqueSecret();
        clientCodeStore.Insert(
            ourCode,
            upstreamTokenId,
            transaction.ClientId,
            transaction.ClientRedirectUri,
            transaction.ClientCodeChallenge,
            now,
            now.AddMinutes(broker.ClientCodeLifetimeMinutes));

        transactionStore.Delete(transaction.TxnId);

        var separator = transaction.ClientRedirectUri.Contains('?') ? '&' : '?';
        var target =
            $"{transaction.ClientRedirectUri}{separator}code={Uri.EscapeDataString(ourCode)}" +
            $"&state={Uri.EscapeDataString(transaction.ClientState)}" +
            $"&iss={Uri.EscapeDataString(broker.IssuerUri)}";

        return Results.Redirect(target);
    }

    private static IResult BadRequest(string error, string errorDescription) =>
        Results.Json(
            new OAuthErrorResponse(error, errorDescription),
            BrokerJsonContext.Default.OAuthErrorResponse,
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult RedirectWithError(string redirectUri, string state, string error)
    {
        var separator = redirectUri.Contains('?') ? '&' : '?';
        var target = $"{redirectUri}{separator}error={Uri.EscapeDataString(error)}";
        if (!string.IsNullOrEmpty(state))
        {
            target += $"&state={Uri.EscapeDataString(state)}";
        }

        return Results.Redirect(target);
    }
}
