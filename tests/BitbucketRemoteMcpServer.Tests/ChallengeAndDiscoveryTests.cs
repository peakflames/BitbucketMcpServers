namespace BitbucketRemoteMcpServer.Tests;

public class ChallengeAndDiscoveryTests : IClassFixture<AuthIntegrationFixture>
{
    private readonly AuthIntegrationFixture _fixture;

    public ChallengeAndDiscoveryTests(AuthIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task UnauthenticatedPost_Returns401WithResourceMetadataChallenge()
    {
        var client = _fixture.Factory.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Bearer", challenge.Scheme);
        Assert.Contains("resource_metadata", challenge.Parameter);
    }

    [Fact]
    public async Task ProtectedResourceMetadata_DescribesThisServerAndTheExternalAs()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/.well-known/oauth-protected-resource/mcp");

        Assert.True(response.IsSuccessStatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(AuthIntegrationFixture.ResourceUri, body.GetProperty("resource").GetString());

        var authServers = body.GetProperty("authorization_servers").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains(_fixture.StubAs.IssuerUrl, authServers);

        var scopes = body.GetProperty("scopes_supported").EnumerateArray().Select(e => e.GetString()).ToArray();
        Assert.Contains("bitbucket:read", scopes);
    }

    [Fact]
    public async Task LegacySseEndpoint_IsStillGoneWithAuthEnabled()
    {
        var client = _fixture.Factory.CreateClient();

        var response = await client.GetAsync("/sse");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
