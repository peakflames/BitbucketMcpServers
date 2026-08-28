namespace BitbucketRemoteMcpServer;

[RequiresUnreferencedCode("Uses Bitbucket API which requires reflection")]
public class Program
{
    [RequiresUnreferencedCode("Uses Bitbucket API which requires reflection")]
    public static int Main(string[] args)
    {
        // Not a general-purpose CLI mode — exists solely so the trimmed, single-file `dotnet
        // publish` output can be verified directly, per Phase 2's stop point. See
        // BrokerStorageSelfTest's own remarks for why building from source can't substitute.
        if (args.Length == 2 && args[0] == "--self-test-broker-storage")
        {
            return Broker.BrokerStorageSelfTest.Run(args[1]);
        }

        try
        {
            var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Verbose() // Capture all log levels
                            .WriteTo.File(Path.Combine(logDir, "BitbucketRemoteMcpServer_.log"),
                                rollingInterval: RollingInterval.Day,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                            .WriteTo.Debug()
                            .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                            .CreateLogger();

            Console.WriteLine("Booting BitbucketRemoteMcpServer...");
            Console.WriteLine($"Logs will be written to: {logDir}");

            var app = BuildApp(args);

            Log.Information("Starting BitbucketRemoteMcpServer...");
            app.Run();

            return 0;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Log.Fatal($"Host terminated unexpectedly. Exception: {ex}");
            Console.ResetColor();
            return 1;
        }
    }

    /// <summary>
    /// Builds the app without running it, so tests can drive it via UseTestServer(). The
    /// `configure` callback runs before any DI registration below (used by tests to install
    /// UseTestServer()/in-memory config/fakes); `postAuthConfigure` runs after AddMcpAuth so
    /// tests can substitute a service (e.g. a fake IBitbucketClientFactory) without racing a
    /// Replace() call it may perform.
    /// </summary>
    [RequiresUnreferencedCode("Uses Bitbucket API which requires reflection")]
    public static WebApplication BuildApp(
        string[] args,
        Action<WebApplicationBuilder>? configure = null,
        Action<WebApplicationBuilder>? postAuthConfigure = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configure?.Invoke(builder);

        // Add HttpContextAccessor to support route data access
        builder.Services.AddHttpContextAccessor();

        // Get the entire application configuration from appsettings.json
        var appConfig = builder.Configuration.Get<BitbucketAppConfig>() ??
                        throw new InvalidOperationException("Application configuration (BitbucketAppConfig) is missing or invalid.");

        var bitbucketConfig = appConfig.BitbucketCloudConfig ??
                               throw new InvalidOperationException("BitbucketCloudConfig configuration section is missing or invalid within BitbucketAppConfig.");

        // Resolve sensitive credentials through IConfiguration (which already includes env vars
        // via the default AddEnvironmentVariables() source) rather than
        // Environment.GetEnvironmentVariable directly, so tests can inject values via
        // ConfigureAppConfiguration without mutating real process-wide env vars.
        var username = builder.Configuration["BITBUCKET_MCP_USERNAME"];
        var apiToken = builder.Configuration["BITBUCKET_MCP_API_TOKEN"];
        var consumerKey = builder.Configuration["BITBUCKET_MCP_CONSUMER_KEY"];
        var secretKey = builder.Configuration["BITBUCKET_MCP_SECRET_KEY"];

        // With Broker:Enabled, BrokerCredentialResolver resolves every caller's own Bitbucket
        // token and never falls back to this shared identity, so it is not required to boot.
        // Without the broker, a shared credential is the only way any tool call authenticates,
        // so it's required as before.
        var brokerEnabledAtBoot = builder.Configuration.GetValue("Broker:Enabled", false);

        if (!brokerEnabledAtBoot)
        {
            if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(apiToken))
            {
                if (string.IsNullOrWhiteSpace(username))
                {
                    throw new InvalidOperationException(
                        "Environment variable 'BITBUCKET_MCP_USERNAME' is not set. " +
                        "Please set this environment variable with your Bitbucket username.");
                }

                if (string.IsNullOrWhiteSpace(apiToken))
                {
                    throw new InvalidOperationException(
                        "Environment variable 'BITBUCKET_MCP_API_TOKEN' is not set. " +
                        "Please set this environment variable with your Bitbucket app password/API token.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(consumerKey))
                {
                    throw new InvalidOperationException(
                        "Environment variable 'BITBUCKET_MCP_CONSUMER_KEY' is not set. " +
                        "Please set this environment variable with your Bitbucket consumer key.");
                }

                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    throw new InvalidOperationException(
                        "Environment variable 'BITBUCKET_MCP_SECRET_KEY' is not set. " +
                        "Please set this environment variable with your Bitbucket secret key.");
                }
            }
        }

        Log.Information("Loaded environment variables for Bitbucket credentials successfully.");

        // Override config values with environment variables
        bitbucketConfig.Username = username ?? string.Empty;
        bitbucketConfig.AppPassword = apiToken ?? string.Empty;
        bitbucketConfig.ConsumerKey = consumerKey ?? string.Empty;
        bitbucketConfig.SecretKey = secretKey ?? string.Empty;

        // Validate the loaded configuration
        if (string.IsNullOrEmpty(bitbucketConfig.AccountName))
        {
            throw new InvalidOperationException("AccountName is required in BitbucketCloudConfig section.");
        }

        // Log information about loaded configuration
        Log.Information("Loaded Bitbucket configuration for Account: {AccountName}", bitbucketConfig.AccountName);

        // Add Serilog
        builder.Services.AddSerilog();

        // Add the configuration and the factory to the DI container
        builder.Services.AddSingleton(bitbucketConfig);
        builder.Services.AddScoped<IBitbucketClientFactory, BitbucketRemoteClientFactory>();

        // Default, byte-identical-to-today credential resolver. AddBroker() below registers
        // BrokerCredentialResolver afterwards when Broker:Enabled — the container resolves the
        // last-registered implementation of a singly-injected interface, so that registration
        // wins without needing a Replace() call here.
        builder.Services.AddScoped<IUpstreamCredentialResolver, SharedCredentialResolver>();

        // Add the McpServer to the DI container
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(o => o.Stateless = true)
            .WithTools<PullRequestTools>()
            .WithTools<RepositoryTools>();

        // Disabled by default: McpAuth adds an OAuth 2.1 resource-server gate in front of /mcp.
        var authEnabled = builder.AddMcpAuth();

        // Also disabled by default: the token-broker storage layer, and the authorization-server
        // endpoints (/authorize, /oauth/callback, /token, DCR built-but-disabled) built on top of
        // it. Registering the SQLite stores here, ahead of the broker logic itself, means Phase 2
        // was verifiable in isolation before Phase 3's endpoints existed.
        var brokerEnabled = builder.AddBroker();

        // Test-only seam: lets tests substitute a service (e.g. a fake IBitbucketClientFactory,
        // or an auth/credential-resolver override) after the registrations above, without
        // needing to race a Replace() call some later phase's Add*() may perform.
        postAuthConfigure?.Invoke(builder);

        var app = builder.Build();

        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
        });

        // UseRouting must run before UseAuthentication/UseAuthorization so that endpoint
        // metadata (RequireAuthorization) is available by the time the authorization middleware
        // runs — without an explicit UseRouting call here, the implicit routing insertion point
        // lands at the first Map* call, which is after these two and would silently turn
        // authorization into a no-op (every request reaches the endpoint unauthenticated).
        app.UseRouting();

        // Stateless transport maps POST-only streamable HTTP; the legacy fake-SSE GET workaround
        // for Cline/TypeScript SDK, and the dual `/`+`/sse`+`/mcp` mount, are removed along with it —
        // `/mcp` is the sole endpoint. BREAKING CHANGE, see CHANGELOG.
        if (authEnabled)
        {
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapMcp("mcp").RequireAuthorization(OAuthScopes.ReadPolicy);
        }
        else
        {
            app.MapMcp("mcp");
        }

        // The broker's own AS endpoints are unauthenticated by design — they issue tokens, they
        // don't consume them. /token is the sole exception in spirit, and it authenticates the
        // caller via the one-time code/refresh token in the request body instead of a bearer
        // header.
        if (brokerEnabled)
        {
            app.MapBrokerEndpoints();
        }

        return app;
    }
}
