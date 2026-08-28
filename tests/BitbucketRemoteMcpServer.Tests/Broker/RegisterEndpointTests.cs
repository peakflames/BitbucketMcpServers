namespace BitbucketRemoteMcpServer.Tests.Broker;

public class RegisterEndpointTests : IClassFixture<BrokerIntegrationFixture>
{
    private readonly BrokerIntegrationFixture _fixture;

    public RegisterEndpointTests(BrokerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Disabled_ByDefault_NeverMapsTheRoute()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.PostAsync(
            "/register", new StringContent("""{"redirect_uris":["http://127.0.0.1:9000/cb"]}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Disabled_ByDefault_MetadataOmitsRegistrationEndpoint()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-authorization-server");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.TryGetProperty("registration_endpoint", out _));
    }

    [Fact]
    public async Task Enabled_RegistersAPublicClient_AndThatClientCanCompleteTheFullFlow()
    {
        using var factory = _fixture.BuildFactoryWithDcrEnabled();
        var client = factory.CreateClient();
        const string redirectUri = "http://127.0.0.1:9000/cb";

        var registerResponse = await client.PostAsync(
            "/register", new StringContent($$"""{"redirect_uris":["{{redirectUri}}"]}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);
        var registration = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientId = registration.GetProperty("client_id").GetString()!;
        Assert.Equal("none", registration.GetProperty("token_endpoint_auth_method").GetString());
        Assert.False(string.IsNullOrEmpty(registration.GetProperty("client_secret").GetString()));

        var metadataResponse = await client.GetAsync("/.well-known/oauth-authorization-server");
        var metadata = await metadataResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(metadata.TryGetProperty("registration_endpoint", out _));

        _fixture.UpstreamOAuth.OnUserInfo("upstream-access-token-1", "account-id-dcr-test", "uuid-dcr-test");

        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeS256Challenge(codeVerifier);

        var authorizeResponse = await client.GetAsync(
            "/authorize" +
            $"?response_type=code&client_id={clientId}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&state=dcr-state&code_challenge={codeChallenge}&code_challenge_method=S256");
        Assert.Equal(HttpStatusCode.Redirect, authorizeResponse.StatusCode);

        var txnId = HttpTestHelpers.ParseQuery(authorizeResponse.Headers.Location!.ToString())["state"];
        var cookie = HttpTestHelpers.ExtractSetCookie(authorizeResponse, "bb_mcp_consent")!;

        var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/oauth/callback?state={Uri.EscapeDataString(txnId)}&code=upstream-code-abc");
        callbackRequest.Headers.Add("Cookie", $"bb_mcp_consent={cookie}");
        var callbackResponse = await client.SendAsync(callbackRequest);
        var clientCode = HttpTestHelpers.ParseQuery(callbackResponse.Headers.Location!.ToString())["code"];

        var tokenResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = clientCode,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = clientId,
                ["code_verifier"] = codeVerifier,
            }),
        });

        Assert.True(tokenResponse.IsSuccessStatusCode);
    }
}
