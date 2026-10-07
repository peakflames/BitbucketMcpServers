namespace BitbucketRemoteMcpServer.Tests.Fakes;

/// <summary>
/// A second loopback listener standing in for the presigned-URL host a Bitbucket redirect points
/// at. It binds a different port than <see cref="FakeBitbucketServer"/>, which SharpBucket
/// classifies as a cross-origin redirect. Every request is answered with a 400 XML body that
/// echoes the received <c>Authorization</c> header in <c>&lt;ArgumentValue&gt;</c>, the way S3
/// reflects a credential it refuses to accept, so a test can tell whether a token reached it and
/// whether that echo then surfaces in a tool result.
/// </summary>
public sealed class FakeS3Server : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly List<string> _authorizationHeaders = [];
    private readonly Lock _lock = new();
    private int _requestCount;

    public string BaseUrl { get; private set; } = string.Empty;

    /// <summary>Every <c>Authorization</c> header value received so far. Empty when none was sent.</summary>
    public IReadOnlyList<string> AuthorizationHeaders
    {
        get
        {
            lock (_lock)
            {
                return [.. _authorizationHeaders];
            }
        }
    }

    /// <summary>Number of requests received, whether or not they carried credentials.</summary>
    public int RequestCount
    {
        get
        {
            lock (_lock)
            {
                return _requestCount;
            }
        }
    }

    private FakeS3Server(WebApplication app) => _app = app;

    public static async Task<FakeS3Server> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        var app = builder.Build();
        var server = new FakeS3Server(app);

        app.Run(context =>
        {
            var authorization = context.Request.Headers.Authorization.ToString();
            lock (server._lock)
            {
                server._requestCount++;
            }

            if (!string.IsNullOrEmpty(authorization))
            {
                lock (server._lock)
                {
                    server._authorizationHeaders.Add(authorization);
                }
            }

            var body = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <Error><Code>InvalidArgument</Code><Message>Only one auth mechanism allowed</Message><ArgumentName>Authorization</ArgumentName><ArgumentValue>{authorization}</ArgumentValue></Error>
                """;

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/xml";
            return context.Response.WriteAsync(body);
        });

        await app.StartAsync();

        server.BaseUrl = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        return server;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
