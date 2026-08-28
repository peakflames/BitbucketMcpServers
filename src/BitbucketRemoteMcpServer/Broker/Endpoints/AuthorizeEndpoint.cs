namespace BitbucketRemoteMcpServer.Broker.Endpoints;

/// <summary>
/// <c>GET /authorize</c> — the client-facing half of the flow. Validates the client and its
/// redirect_uri first, since only after that can a failure be reported by redirecting the
/// browser rather than a bare error page. Generates this server's own upstream PKCE pair (never
/// the client's — see the "two PKCE pairs, always" rule) and 302s to Bitbucket with
/// <c>state=txn_id</c>. Deliberately never forwards a client-supplied <c>resource</c> parameter
/// upstream — Bitbucket Cloud rejects RFC 8707 resource indicators with <c>invalid_target</c>;
/// it is recorded on the transaction and bound to this server's own issued JWT <c>aud</c> at
/// <c>/token</c> instead.
/// </summary>
public static class AuthorizeEndpoint
{
    public static IResult Handle(
        HttpContext httpContext,
        string? response_type,
        string? client_id,
        string? redirect_uri,
        string? state,
        string? code_challenge,
        string? code_challenge_method,
        string? scope,
        string? resource,
        IOptions<BrokerOptions> brokerOptions,
        ClientLookup clientLookup,
        OAuthTransactionStore transactionStore)
    {
        if (string.IsNullOrWhiteSpace(client_id))
        {
            return Results.BadRequest(new { error = "invalid_request", error_description = "client_id is required." });
        }

        var client = clientLookup.TryGet(client_id);
        if (client is null)
        {
            return Results.BadRequest(new { error = "invalid_client", error_description = "Unknown client_id." });
        }

        if (string.IsNullOrWhiteSpace(redirect_uri) || !RedirectUriValidator.IsValid(redirect_uri, client.RedirectUris))
        {
            return Results.BadRequest(new
            {
                error = "invalid_request",
                error_description = "redirect_uri is missing or not registered for this client.",
            });
        }

        // Only past this point is redirect_uri trusted enough to report errors to via a redirect
        // (RFC 6749 §4.1.2.1) instead of a bare 400.
        if (!string.Equals(response_type, "code", StringComparison.Ordinal))
        {
            return RedirectWithError(redirect_uri, state, "unsupported_response_type");
        }

        if (string.IsNullOrWhiteSpace(code_challenge) || !string.Equals(code_challenge_method, "S256", StringComparison.Ordinal))
        {
            return RedirectWithError(redirect_uri, state, "invalid_request");
        }

        var broker = brokerOptions.Value;
        var now = DateTimeOffset.UtcNow;
        var txnId = Guid.NewGuid().ToString("N");
        var upstreamCodeVerifier = PkceHelper.GenerateCodeVerifier();
        var upstreamCodeChallenge = PkceHelper.ComputeS256Challenge(upstreamCodeVerifier);
        var lifetime = TimeSpan.FromMinutes(broker.TransactionLifetimeMinutes);
        var consentSecret = ConsentCookie.Issue(httpContext, txnId, lifetime);

        transactionStore.Insert(new OAuthTransaction(
            TxnId: txnId,
            ClientId: client_id,
            ClientRedirectUri: redirect_uri,
            ClientState: state ?? string.Empty,
            ClientCodeChallenge: code_challenge,
            UpstreamCodeVerifier: upstreamCodeVerifier,
            Scopes: scope ?? string.Empty,
            Resource: resource,
            ConsentTokenHash: TokenHashing.Hash(consentSecret),
            CreatedAt: now,
            ExpiresAt: now.Add(lifetime)));

        var callbackUrl = $"{broker.IssuerUri}/oauth/callback";
        var upstreamScope = string.Join(' ', broker.UpstreamScopes);
        var upstreamAuthorizeUrl =
            $"{broker.UpstreamAuthorizeUrl}" +
            $"?client_id={Uri.EscapeDataString(broker.UpstreamClientId)}" +
            "&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
            $"&state={Uri.EscapeDataString(txnId)}" +
            $"&scope={Uri.EscapeDataString(upstreamScope)}" +
            $"&code_challenge={Uri.EscapeDataString(upstreamCodeChallenge)}" +
            "&code_challenge_method=S256";

        return Results.Redirect(upstreamAuthorizeUrl);
    }

    private static IResult RedirectWithError(string redirectUri, string? state, string error)
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
