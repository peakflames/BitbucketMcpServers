namespace BitbucketRemoteMcpServer.Broker.Endpoints;

/// <summary>
/// Binds the browser session that started an authorization attempt to the one completing it at
/// <c>/oauth/callback</c> — the confused-deputy defense the FastMCP <c>OAuthProxy</c> model calls
/// the consent-binding cookie. Without it, a second browser session that learns another user's
/// still-pending <c>txn_id</c> (e.g. from a leaked Referer header) could complete that user's
/// authorization and be handed their Bitbucket credential.
/// </summary>
internal static class ConsentCookie
{
    private const string CookieName = "bb_mcp_consent";

    /// <summary>Issues the cookie for a new transaction and returns the secret whose hash was
    /// stored on <see cref="OAuthTransaction.ConsentTokenHash"/>.</summary>
    public static string Issue(HttpContext httpContext, string txnId, TimeSpan lifetime)
    {
        var secret = PkceHelper.GenerateOpaqueSecret();
        httpContext.Response.Cookies.Append(CookieName, $"{txnId}.{secret}", new CookieOptions
        {
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            MaxAge = lifetime,
        });
        return secret;
    }

    public static bool Validate(HttpContext httpContext, string txnId, string expectedConsentTokenHash)
    {
        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var raw) || raw is null)
            return false;

        var separatorIndex = raw.IndexOf('.');
        if (separatorIndex < 0)
            return false;

        var cookieTxnId = raw[..separatorIndex];
        var secret = raw[(separatorIndex + 1)..];

        return string.Equals(cookieTxnId, txnId, StringComparison.Ordinal)
            && TokenHashing.Verify(secret, expectedConsentTokenHash);
    }

    public static void Clear(HttpContext httpContext) => httpContext.Response.Cookies.Delete(CookieName);
}
