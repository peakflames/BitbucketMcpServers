namespace BitbucketRemoteMcpServer.Broker;

public sealed record UpstreamTokenResponse(
    string AccessToken, string? RefreshToken, string TokenType, int ExpiresInSeconds, string? Scope);

/// <summary>
/// The upstream (Bitbucket) leg of the broker, called directly with <see cref="HttpClient"/>
/// rather than through SharpBucket's <c>OAuth2TokenProvider</c> — that type's token URL is a
/// private hardcoded constant pointed at <c>bitbucket.org</c>, with no constructor seam to
/// redirect it at a fake server for tests, and changing that would mean another SharpBucket fork
/// PR. <see cref="BrokerOptions.UpstreamTokenUrl"/>/<see cref="BrokerOptions.UpstreamUserInfoUrl"/>
/// exist precisely so this client can be pointed at a fake in tests instead.
/// </summary>
public sealed class UpstreamOAuthClient(
    IHttpClientFactory httpClientFactory,
    IOptions<BrokerOptions> brokerOptions,
    ILogger<UpstreamOAuthClient> logger)
{
    public const string HttpClientName = "BitbucketUpstreamOAuth";

    public Task<UpstreamTokenResponse?> ExchangeAuthorizationCodeAsync(
        string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
        };
        return PostTokenRequestAsync(form, cancellationToken);
    }

    public Task<UpstreamTokenResponse?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };
        return PostTokenRequestAsync(form, cancellationToken);
    }

    /// <summary>Resolves which Bitbucket account a freshly exchanged access token belongs to.
    /// Prefers <c>account_id</c> (the modern stable identifier); falls back to <c>uuid</c>.
    /// Necessary because Bitbucket's OAuth2 token response carries no ID token — the
    /// "two different humans, same call, different results" requirement needs a real per-user
    /// subject, not a placeholder.</summary>
    public async Task<string?> GetSubjectAsync(string accessToken, CancellationToken cancellationToken)
    {
        var broker = brokerOptions.Value;
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Get, broker.UpstreamUserInfoUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Upstream user-info request to {Url} failed.", broker.UpstreamUserInfoUrl);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Upstream user-info request to {Url} returned {StatusCode}.",
                broker.UpstreamUserInfoUrl, response.StatusCode);
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (root.TryGetProperty("account_id", out var accountId) && accountId.ValueKind == JsonValueKind.String)
            return accountId.GetString();
        if (root.TryGetProperty("uuid", out var uuid) && uuid.ValueKind == JsonValueKind.String)
            return uuid.GetString();

        return null;
    }

    private async Task<UpstreamTokenResponse?> PostTokenRequestAsync(
        Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        var broker = brokerOptions.Value;
        var client = httpClientFactory.CreateClient(HttpClientName);
        using var request = new HttpRequestMessage(HttpMethod.Post, broker.UpstreamTokenUrl)
        {
            Content = new FormUrlEncodedContent(form),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{broker.UpstreamClientId}:{broker.UpstreamClientSecret}")));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Upstream OAuth token request to {TokenUrl} failed.", broker.UpstreamTokenUrl);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Upstream OAuth token request to {TokenUrl} returned {StatusCode}.",
                broker.UpstreamTokenUrl, response.StatusCode);
            return null;
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        if (!root.TryGetProperty("access_token", out var accessTokenElement))
            return null;

        return new UpstreamTokenResponse(
            AccessToken: accessTokenElement.GetString()!,
            RefreshToken: root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : null,
            TokenType: root.TryGetProperty("token_type", out var tt) ? tt.GetString() ?? "Bearer" : "Bearer",
            ExpiresInSeconds: root.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600,
            Scope: root.TryGetProperty("scope", out var sc) ? sc.GetString() : null);
    }
}
