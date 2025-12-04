using System.Diagnostics.CodeAnalysis;
using BitbucketMcpTools;
using Serilog;

namespace BitbucketRemoteMcpServer;

[RequiresUnreferencedCode("Uses Bitbucket API which requires reflection")]
public class Program
{
    [RequiresUnreferencedCode("Uses Bitbucket API which requires reflection")]
    public static int Main(string[] args)
    {
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

            // Create the DI container
            var builder = WebApplication.CreateBuilder(args);

            // Add HttpContextAccessor to support route data access
            builder.Services.AddHttpContextAccessor();

            // Get the entire application configuration from appsettings.json
            var appConfig = builder.Configuration.Get<BitbucketAppConfig>() ??
                            throw new InvalidOperationException("Application configuration (BitbucketAppConfig) is missing or invalid.");

            var bitbucketConfig = appConfig.BitbucketCloudConfig ??
                                   throw new InvalidOperationException("BitbucketCloudConfig configuration section is missing or invalid within BitbucketAppConfig.");

            // Resolve environment variables for sensitive credentials
            var username = Environment.GetEnvironmentVariable("BITBUCKET_MCP_USERNAME");
            var apiToken = Environment.GetEnvironmentVariable("BITBUCKET_MCP_API_TOKEN");

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

            Log.Information("Loaded environment variables for Bitbucket credentials successfully.");

            // Override config values with environment variables
            bitbucketConfig.Username = username;
            bitbucketConfig.AppPassword = apiToken;

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

            // Add the McpServer to the DI container
            builder.Services
                .AddMcpServer()
                .WithHttpTransport()
                .WithTools<PullRequestTools>();

            // Build and Run the McpServer
            Log.Information("Starting BitbucketRemoteMcpServer...");
            var app = builder.Build();

            // Map MCP endpoints with route parameter
            app.MapMcp();

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

}
