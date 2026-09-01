namespace BitbucketRemoteMcpServer.Tests.Broker;

public class AuthorizeEndpointTests : IClassFixture<BrokerIntegrationFixture>
{
    private readonly BrokerIntegrationFixture _fixture;

    public AuthorizeEndpointTests(BrokerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private static string BuildAuthorizeUrl(string clientId, string redirectUri, string state = "client-state-1") =>
        "/authorize" +
        $"?response_type=code&client_id={Uri.EscapeDataString(clientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        $"&state={Uri.EscapeDataString(state)}" +
        "&code_challenge=client-challenge-abc&code_challenge_method=S256";

    [Fact]
    public async Task UnknownClientId_Returns400()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync(
            BuildAuthorizeUrl("no-such-client", BrokerIntegrationFixture.ClientRedirectUri));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnregisteredRedirectUri_Returns400()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync(
            BuildAuthorizeUrl(BrokerIntegrationFixture.ClientId, "http://127.0.0.1:9999/not-registered"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task LoopbackRedirectUri_MatchesRegardlessOfPort()
    {
        var client = _fixture.Factory.CreateClient();

        // ClientRedirectUri is registered as http://127.0.0.1:54321/callback; RFC 8252 §7.3 says a
        // native app's loopback redirect may use any port at runtime.
        var response = await client.GetAsync(
            BuildAuthorizeUrl(BrokerIntegrationFixture.ClientId, "http://127.0.0.1:61234/callback"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task ValidRequest_RedirectsUpstreamWithOurOwnPkcePairAndNoResourceParameter()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync(
            BuildAuthorizeUrl(BrokerIntegrationFixture.ClientId, BrokerIntegrationFixture.ClientRedirectUri));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();

        Assert.StartsWith(_fixture.UpstreamOAuth.BaseUrl, location, StringComparison.Ordinal);

        var query = HttpTestHelpers.ParseQuery(location);
        Assert.Equal("our-consumer-key", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        // The upstream code_challenge must be OUR OWN pair, never derived from the client's
        // code_challenge — the "two PKCE pairs, always" rule.
        Assert.NotEqual("client-challenge-abc", query["code_challenge"]);
        Assert.False(query.ContainsKey("resource"));

        var stateParam = query["state"];
        Assert.False(string.IsNullOrEmpty(stateParam));
        Assert.NotEqual("client-state-1", stateParam); // upstream state is our txn_id, not the client's

        Assert.NotNull(HttpTestHelpers.ExtractSetCookie(response, "bb_mcp_consent"));
    }

    [Fact]
    public async Task MissingCodeChallenge_RedirectsBackToClientWithInvalidRequestError()
    {
        var client = _fixture.Factory.CreateClient();

        var url = "/authorize" +
            $"?response_type=code&client_id={BrokerIntegrationFixture.ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(BrokerIntegrationFixture.ClientRedirectUri)}" +
            "&state=client-state-2";

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith(BrokerIntegrationFixture.ClientRedirectUri, location, StringComparison.Ordinal);

        var query = HttpTestHelpers.ParseQuery(location);
        Assert.Equal("invalid_request", query["error"]);
        Assert.Equal("client-state-2", query["state"]);
    }
}
