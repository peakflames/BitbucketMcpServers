namespace BitbucketRemoteMcpServer.Tests.Broker;

public class RedirectUriValidatorTests
{
    [Fact]
    public void ExactMatch_IsValid()
    {
        Assert.True(RedirectUriValidator.IsValid(
            "https://client.example.invalid/callback", ["https://client.example.invalid/callback"]));
    }

    [Fact]
    public void DifferentPath_IsInvalid()
    {
        Assert.False(RedirectUriValidator.IsValid(
            "https://client.example.invalid/other", ["https://client.example.invalid/callback"]));
    }

    [Fact]
    public void DifferentPort_ForANonLoopbackHost_IsInvalid()
    {
        Assert.False(RedirectUriValidator.IsValid(
            "https://client.example.invalid:9999/callback", ["https://client.example.invalid:8080/callback"]));
    }

    [Theory]
    [InlineData("http://127.0.0.1:1/callback")]
    [InlineData("http://127.0.0.1:65000/callback")]
    public void LoopbackHost_MatchesAnyPort(string candidate)
    {
        // RFC 8252 §7.3 — a native app's loopback redirect legitimately picks an ephemeral port
        // at runtime that registration cannot pin in advance.
        Assert.True(RedirectUriValidator.IsValid(candidate, ["http://127.0.0.1:54321/callback"]));
    }

    [Fact]
    public void LoopbackHost_StillRequiresAPathMatch()
    {
        Assert.False(RedirectUriValidator.IsValid(
            "http://127.0.0.1:9999/different-path", ["http://127.0.0.1:54321/callback"]));
    }

    [Fact]
    public void UserInfoTrick_DoesNotFoolTheHostComparison()
    {
        // Uri parses "localhost" here as UserInfo, not Host — the real host is evil.invalid.
        Assert.False(RedirectUriValidator.IsValid(
            "http://localhost@evil.invalid/callback", ["http://localhost/callback"]));
    }

    [Fact]
    public void NonAbsoluteCandidate_IsInvalid()
    {
        Assert.False(RedirectUriValidator.IsValid("not-a-uri", ["https://client.example.invalid/callback"]));
    }
}
