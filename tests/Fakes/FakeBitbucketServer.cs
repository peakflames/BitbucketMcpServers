namespace BitbucketRemoteMcpServer.Tests.Fakes;

/// <summary>
/// A real Kestrel listener on a loopback/dynamic port that answers the handful of Bitbucket
/// Cloud REST endpoints the 11 tools actually call. Not an in-memory TestServer: SharpBucket
/// (via RestSharp) makes real socket calls and has no supported way to redirect onto an
/// in-process test pipeline. Reachable because <see cref="BitbucketClient"/> accepts an optional
/// baseUrl override that SharpBucketV2's own public constructor already supports.
/// </summary>
public sealed class FakeBitbucketServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly Dictionary<string, string> _repositories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _commits = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _branches = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _workspaces = new(StringComparer.Ordinal);
    private readonly List<string> _authorizationHeaders = [];
    private readonly Lock _authorizationHeadersLock = new();

    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>
    /// Every <c>Authorization</c> header value received so far, in arrival order. Lets a test
    /// assert what SharpBucket actually put on the wire rather than what it was configured with.
    /// Requests that carried no such header contribute nothing.
    /// </summary>
    public IReadOnlyList<string> AuthorizationHeaders
    {
        get
        {
            lock (_authorizationHeadersLock)
            {
                return [.. _authorizationHeaders];
            }
        }
    }

    private FakeBitbucketServer(WebApplication app) => _app = app;

    public static async Task<FakeBitbucketServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var server = new FakeBitbucketServer(app);

        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue("Authorization", out var authorization))
            {
                lock (server._authorizationHeadersLock)
                {
                    server._authorizationHeaders.Add(authorization.ToString());
                }
            }

            await next(context);
        });

        // No "/2.0" prefix here: SharpBucketV2(baseUrl) treats the whole baseUrl as the API
        // root (production's default already bakes "/2.0" into SharpBucketV2.BITBUCKET_URL), so
        // BaseUrl passed to this server must be the equivalent of "https://api.bitbucket.org/2.0".
        app.MapGet("/repositories/{account}/{repo}", (string account, string repo) =>
            server.Respond(server._repositories, $"{account}/{repo}"));

        app.MapGet("/repositories/{account}/{repo}/commit/{revision}", (string account, string repo, string revision) =>
            server.Respond(server._commits, $"{account}/{repo}/{revision}"));

        app.MapGet("/repositories/{account}/{repo}/refs/branches", (string account, string repo) =>
            server.Respond(server._branches, $"{account}/{repo}"));

        app.MapGet("/workspaces/{workspace}", (string workspace) =>
            server.Respond(server._workspaces, workspace));

        await app.StartAsync();

        server.BaseUrl = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        return server;
    }

    public void OnRepository(string accountName, string repoSlug, string json) =>
        _repositories[$"{accountName}/{repoSlug}"] = json;

    public void OnCommit(string accountName, string repoSlug, string revision, string json) =>
        _commits[$"{accountName}/{repoSlug}/{revision}"] = json;

    public void OnBranches(string accountName, string repoSlug, string json) =>
        _branches[$"{accountName}/{repoSlug}"] = json;

    public void OnWorkspace(string accountName, string json) =>
        _workspaces[accountName] = json;

    private IResult Respond(Dictionary<string, string> table, string key) =>
        table.TryGetValue(key, out var json)
            ? Results.Text(json, "application/json")
            : Results.NotFound(new { type = "error", error = new { message = $"Not found: {key}" } });

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
