namespace BitbucketRemoteMcpServer.Tests.Fakes;

/// <summary>
/// A real Kestrel listener standing in for Bitbucket's own OAuth token endpoint and user-identity
/// endpoint — the broker's <c>UpstreamOAuthClient</c> talks to it with a real
/// <see cref="HttpClient"/>, the same as it would talk to bitbucket.org, so
/// <c>Broker:UpstreamTokenUrl</c>/<c>Broker:UpstreamUserInfoUrl</c> exist precisely to redirect it
/// here in tests. Every request received is recorded so tests can assert what the broker actually
/// sent upstream (Basic-auth client credentials, the server's own PKCE code_verifier, never the
/// client's — and never a `resource` parameter).
/// </summary>
public sealed class FakeUpstreamOAuthServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly Dictionary<string, string> _userInfoByAccessToken = new(StringComparer.Ordinal);
    private readonly List<string> _authorizationHeaders = [];
    private readonly List<Dictionary<string, string>> _tokenRequestForms = [];
    private readonly Lock _lock = new();

    public string BaseUrl { get; private set; } = string.Empty;

    public string TokenUrl => $"{BaseUrl}/site/oauth2/access_token";

    public string UserInfoUrl => $"{BaseUrl}/2.0/user";

    /// <summary>When true, the next (and only the next) token exchange responds 400 invalid_grant
    /// — simulates Bitbucket rejecting a code or refresh token.</summary>
    public bool NextExchangeShouldFail { get; set; }

    public string NextAccessToken { get; set; } = "upstream-access-token-1";

    public string? NextRefreshToken { get; set; } = "upstream-refresh-token-1";

    public int NextExpiresInSeconds { get; set; } = 3600;

    public IReadOnlyList<string> AuthorizationHeaders
    {
        get { lock (_lock) { return [.. _authorizationHeaders]; } }
    }

    public IReadOnlyList<Dictionary<string, string>> TokenRequestForms
    {
        get { lock (_lock) { return [.. _tokenRequestForms]; } }
    }

    private FakeUpstreamOAuthServer(WebApplication app) => _app = app;

    public static async Task<FakeUpstreamOAuthServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var server = new FakeUpstreamOAuthServer(app);

        app.MapPost("/site/oauth2/access_token", async (HttpContext context) =>
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var authorization))
            {
                lock (server._lock)
                {
                    server._authorizationHeaders.Add(authorization.ToString());
                }
            }

            var form = await context.Request.ReadFormAsync();
            var formDictionary = form.Keys.ToDictionary(key => key, key => form[key].ToString(), StringComparer.Ordinal);
            lock (server._lock)
            {
                server._tokenRequestForms.Add(formDictionary);
            }

            if (server.NextExchangeShouldFail)
            {
                server.NextExchangeShouldFail = false;
                return Results.BadRequest(new { error = "invalid_grant" });
            }

            var response = new Dictionary<string, object?>
            {
                ["access_token"] = server.NextAccessToken,
                ["token_type"] = "Bearer",
                ["expires_in"] = server.NextExpiresInSeconds,
                ["scope"] = "account repository pullrequest",
            };
            if (server.NextRefreshToken is not null)
            {
                response["refresh_token"] = server.NextRefreshToken;
            }

            return Results.Json(response);
        });

        app.MapGet("/2.0/user", (HttpContext context) =>
        {
            if (!context.Request.Headers.TryGetValue("Authorization", out var authorization) ||
                !authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal))
            {
                return Results.Unauthorized();
            }

            var accessToken = authorization.ToString()["Bearer ".Length..];
            return server._userInfoByAccessToken.TryGetValue(accessToken, out var json)
                ? Results.Text(json, "application/json")
                : Results.NotFound();
        });

        await app.StartAsync();

        server.BaseUrl = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        return server;
    }

    public void OnUserInfo(string accessToken, string accountId, string uuid) =>
        _userInfoByAccessToken[accessToken] =
            $$"""{"account_id": "{{accountId}}", "uuid": "{{{uuid}}}"}""";

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
