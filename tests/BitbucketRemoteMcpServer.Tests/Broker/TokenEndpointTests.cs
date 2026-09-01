namespace BitbucketRemoteMcpServer.Tests.Broker;

public class TokenEndpointTests : IClassFixture<BrokerIntegrationFixture>
{
    private readonly BrokerIntegrationFixture _fixture;

    public TokenEndpointTests(BrokerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> GetClientCodeAsync(HttpClient client, string codeChallenge)
    {
        var authorizeUrl = "/authorize" +
            $"?response_type=code&client_id={BrokerIntegrationFixture.ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(BrokerIntegrationFixture.ClientRedirectUri)}" +
            $"&state=client-state-1&code_challenge={codeChallenge}&code_challenge_method=S256";

        var authorizeResponse = await client.GetAsync(authorizeUrl);
        var txnId = HttpTestHelpers.ParseQuery(authorizeResponse.Headers.Location!.ToString())["state"];
        var cookie = HttpTestHelpers.ExtractSetCookie(authorizeResponse, "bb_mcp_consent")!;

        var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/oauth/callback?state={Uri.EscapeDataString(txnId)}&code=upstream-code-abc");
        callbackRequest.Headers.Add("Cookie", $"bb_mcp_consent={cookie}");
        var callbackResponse = await client.SendAsync(callbackRequest);

        return HttpTestHelpers.ParseQuery(callbackResponse.Headers.Location!.ToString())["code"];
    }

    private static HttpRequestMessage TokenRequest(Dictionary<string, string> form) =>
        new(HttpMethod.Post, "/token") { Content = new FormUrlEncodedContent(form) };

    [Fact]
    public async Task AuthorizationCodeGrant_IssuesAJwtThatAuthorizesMcp()
    {
        var client = _fixture.Factory.CreateClient();
        _fixture.UpstreamOAuth.OnUserInfo("upstream-access-token-1", "account-id-jwt-test", "uuid-jwt-test");

        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeS256Challenge(codeVerifier);
        var code = await GetClientCodeAsync(client, codeChallenge);

        var response = await client.SendAsync(TokenRequest(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = BrokerIntegrationFixture.ClientRedirectUri,
            ["client_id"] = BrokerIntegrationFixture.ClientId,
            ["code_verifier"] = codeVerifier,
        }));

        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Bearer", body.GetProperty("token_type").GetString());
        var accessToken = body.GetProperty("access_token").GetString()!;
        Assert.False(string.IsNullOrEmpty(body.GetProperty("refresh_token").GetString()));

        using var mcpRequest = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        mcpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        mcpRequest.Headers.Accept.ParseAdd("application/json");
        mcpRequest.Headers.Accept.ParseAdd("text/event-stream");

        var mcpResponse = await client.SendAsync(mcpRequest);

        Assert.NotEqual(HttpStatusCode.Unauthorized, mcpResponse.StatusCode);
    }

    [Fact]
    public async Task ReplayedClientCode_IsRejected()
    {
        var client = _fixture.Factory.CreateClient();
        _fixture.UpstreamOAuth.OnUserInfo("upstream-access-token-1", "account-id-replay-test", "uuid-replay-test");

        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeS256Challenge(codeVerifier);
        var code = await GetClientCodeAsync(client, codeChallenge);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = BrokerIntegrationFixture.ClientRedirectUri,
            ["client_id"] = BrokerIntegrationFixture.ClientId,
            ["code_verifier"] = codeVerifier,
        };

        var first = await client.SendAsync(TokenRequest(new Dictionary<string, string>(form)));
        Assert.True(first.IsSuccessStatusCode);

        var second = await client.SendAsync(TokenRequest(new Dictionary<string, string>(form)));
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task WrongCodeVerifier_IsRejected()
    {
        var client = _fixture.Factory.CreateClient();
        _fixture.UpstreamOAuth.OnUserInfo("upstream-access-token-1", "account-id-wrong-verifier-test", "uuid-wrong-verifier-test");

        var realVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeS256Challenge(realVerifier);
        var code = await GetClientCodeAsync(client, codeChallenge);

        var response = await client.SendAsync(TokenRequest(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = BrokerIntegrationFixture.ClientRedirectUri,
            ["client_id"] = BrokerIntegrationFixture.ClientId,
            ["code_verifier"] = PkceHelper.GenerateCodeVerifier(), // not the verifier matching codeChallenge
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenGrant_RotatesTheRefreshTokenAndIssuesANewJwt()
    {
        var client = _fixture.Factory.CreateClient();
        _fixture.UpstreamOAuth.OnUserInfo("upstream-access-token-1", "account-id-refresh-test", "uuid-refresh-test");

        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeS256Challenge(codeVerifier);
        var code = await GetClientCodeAsync(client, codeChallenge);

        var tokenResponse = await client.SendAsync(TokenRequest(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = BrokerIntegrationFixture.ClientRedirectUri,
            ["client_id"] = BrokerIntegrationFixture.ClientId,
            ["code_verifier"] = codeVerifier,
        }));
        var tokenBody = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstAccessToken = tokenBody.GetProperty("access_token").GetString()!;
        var firstRefreshToken = tokenBody.GetProperty("refresh_token").GetString()!;

        var refreshResponse = await client.SendAsync(TokenRequest(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = firstRefreshToken,
            ["client_id"] = BrokerIntegrationFixture.ClientId,
        }));

        Assert.True(refreshResponse.IsSuccessStatusCode);
        var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondAccessToken = refreshBody.GetProperty("access_token").GetString()!;
        var secondRefreshToken = refreshBody.GetProperty("refresh_token").GetString()!;

        Assert.NotEqual(firstAccessToken, secondAccessToken);
        Assert.NotEqual(firstRefreshToken, secondRefreshToken);

        // Rotation: the old refresh token must no longer work.
        var replayResponse = await client.SendAsync(TokenRequest(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = firstRefreshToken,
            ["client_id"] = BrokerIntegrationFixture.ClientId,
        }));
        Assert.Equal(HttpStatusCode.BadRequest, replayResponse.StatusCode);
    }
}
