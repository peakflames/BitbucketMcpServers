namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Plain WebApplicationFactory&lt;Program&gt; for transport-level tests that don't need to
/// override any service — reuses the project's own appsettings.json (AccountName ships a
/// placeholder value there) and only needs the credential env vars Program.BuildApp requires to
/// not throw at boot.
/// </summary>
public class TestServerFactory : WebApplicationFactory<Program>
{
    public TestServerFactory()
    {
        Environment.SetEnvironmentVariable("BITBUCKET_MCP_USERNAME", "fake-user");
        Environment.SetEnvironmentVariable("BITBUCKET_MCP_API_TOKEN", "fake-app-password");
    }
}
