namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Drives Program.BuildApp directly with UseTestServer() — config is injected via
/// ConfigureAppConfiguration/AddInMemoryCollection rather than by mutating process-wide
/// environment variables, which Program.BuildApp's credential resolution reads through
/// IConfiguration for exactly this reason.
/// </summary>
public sealed class BitbucketMcpServerFactory : IDisposable
{
    private readonly Dictionary<string, string?> _configValues = new(StringComparer.Ordinal);
    private Action<IServiceCollection>? _postConfigureServices;
    private string? _environmentName;
    private WebApplication? _app;

    public BitbucketMcpServerFactory()
    {
        With("BitbucketCloudConfig:AccountName", "fake-workspace");
        With("BITBUCKET_MCP_USERNAME", "fake-user");
        With("BITBUCKET_MCP_API_TOKEN", "fake-app-password");
    }

    public BitbucketMcpServerFactory With(string key, string? value)
    {
        _configValues[key] = value;
        return this;
    }

    /// <summary>Runs after every registration in BuildApp — the only seam that can safely
    /// override a service (e.g. IBitbucketClientFactory) without racing a later Add*() call.</summary>
    public BitbucketMcpServerFactory WithPostConfigureServices(Action<IServiceCollection> configure)
    {
        _postConfigureServices = _postConfigureServices is null ? configure : _postConfigureServices + configure;
        return this;
    }

    /// <summary>McpAuthOptionsValidator only allows a plaintext http Issuer in Development.
    /// Passed as a `--environment` command-line argument to Program.BuildApp, not set via
    /// builder.Environment.EnvironmentName after the fact — WebApplicationBuilder combined with
    /// WebHost.UseTestServer() re-derives IHostEnvironment from host configuration during
    /// Build(), silently discarding a post-hoc mutation of the Environment property. The
    /// command-line switch is read during WebApplication.CreateBuilder(args) itself, before
    /// anything else runs, so it is not subject to that override.</summary>
    public BitbucketMcpServerFactory WithEnvironment(string environmentName)
    {
        _environmentName = environmentName;
        return this;
    }

    public IServiceProvider Services => GetOrBuildApp().Services;

    public HttpClient CreateClient() => GetOrBuildApp().GetTestClient();

    private WebApplication GetOrBuildApp()
    {
        if (_app is not null)
            return _app;

        var args = _environmentName is not null
            ? new[] { "--environment", _environmentName }
            : [];

        _app = Program.BuildApp(
            args,
            builder =>
            {
                builder.WebHost.UseTestServer();
                builder.Configuration.AddInMemoryCollection(_configValues);
            },
            builder =>
            {
                _postConfigureServices?.Invoke(builder.Services);
            });

        _app.Start();
        return _app;
    }

    public void Dispose()
    {
        if (_app is null)
            return;

        _app.StopAsync().GetAwaiter().GetResult();
        _app.DisposeAsync().GetAwaiter().GetResult();
    }
}
