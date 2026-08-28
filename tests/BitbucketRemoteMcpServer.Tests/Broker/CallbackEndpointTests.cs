namespace BitbucketRemoteMcpServer.Tests.Broker;

public class CallbackEndpointTests : IClassFixture<BrokerIntegrationFixture>
{
    private readonly BrokerIntegrationFixture _fixture;

    public CallbackEndpointTests(BrokerIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<(string TxnId, string ConsentCookie)> StartAuthorizationAsync(HttpClient client)
    {
        var url = "/authorize" +
            $"?response_type=code&client_id={BrokerIntegrationFixture.ClientId}" +
            $"&redirect_uri={Uri.EscapeDataString(BrokerIntegrationFixture.ClientRedirectUri)}" +
            "&state=client-state-1&code_challenge=client-challenge-abc&code_challenge_method=S256";

        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var query = HttpTestHelpers.ParseQuery(response.Headers.Location!.ToString());
        var cookie = HttpTestHelpers.ExtractSetCookie(response, "bb_mcp_consent")!;
        return (query["state"], cookie);
    }

    private static HttpRequestMessage CallbackRequest(string txnId, string cookie, string? upstreamCode = "upstream-code-abc")
    {
        var url = $"/oauth/callback?state={Uri.EscapeDataString(txnId)}";
        if (upstreamCode is not null)
        {
            url += $"&code={Uri.EscapeDataString(upstreamCode)}";
        }

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"bb_mcp_consent={cookie}");
        return request;
    }

    [Fact]
    public async Task ValidCallback_RedirectsToClientWithCodeStateAndIss()
    {
        var client = _fixture.Factory.CreateClient();
        _fixture.UpstreamOAuth.OnUserInfo("upstream-access-token-1", "account-id-1", "uuid-1");

        var (txnId, cookie) = await StartAuthorizationAsync(client);
        var response = await client.SendAsync(CallbackRequest(txnId, cookie));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = response.Headers.Location!.ToString();
        Assert.StartsWith(BrokerIntegrationFixture.ClientRedirectUri, location, StringComparison.Ordinal);

        var query = HttpTestHelpers.ParseQuery(location);
        Assert.False(string.IsNullOrEmpty(query["code"]));
        Assert.Equal("client-state-1", query["state"]);
        Assert.Equal(BrokerIntegrationFixture.IssuerUri, query["iss"]);
    }

    [Fact]
    public async Task MismatchedConsentCookie_Returns403AndDeletesTheTransaction()
    {
        var client = _fixture.Factory.CreateClient();

        var (txnId, _) = await StartAuthorizationAsync(client);

        // A different transaction's cookie (or none at all) must not authorize this callback.
        var response = await client.SendAsync(CallbackRequest(txnId, "not-the-real-secret"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // The transaction is now burned even though the real cookie was never presented — the
        // callback endpoint deletes on the first mismatch rather than allowing retries. A second
        // attempt sees no transaction at all rather than getting another chance to guess the cookie.
        var secondAttemptResponse = await client.SendAsync(CallbackRequest(txnId, "not-the-real-secret"));
        Assert.Equal(HttpStatusCode.BadRequest, secondAttemptResponse.StatusCode);
    }

    [Fact]
    public async Task UnknownState_Returns400()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/oauth/callback?code=upstream-code&state=no-such-transaction");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpstreamExchangeFailure_RedirectsToClientWithServerError()
    {
        var client = _fixture.Factory.CreateClient();
        _fixture.UpstreamOAuth.NextExchangeShouldFail = true;

        var (txnId, cookie) = await StartAuthorizationAsync(client);
        var response = await client.SendAsync(CallbackRequest(txnId, cookie));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = HttpTestHelpers.ParseQuery(response.Headers.Location!.ToString());
        Assert.Equal("server_error", query["error"]);
        Assert.Equal("client-state-1", query["state"]);
    }
}
