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

            var bitbucketProjects = appConfig.BitbucketCloudConfig ??
                                   throw new InvalidOperationException("BitbucketCloudConfig configuration section is missing or invalid within BitbucketAppConfig.");

            // Validate the loaded project configurations
            if (!bitbucketProjects.Any())
            {
                throw new InvalidOperationException("No Bitbucket projects configured in BitbucketCloudConfig section.");
            }
            if (bitbucketProjects.Count(p => p.Default) > 1)
            {
                throw new InvalidOperationException("Multiple Bitbucket projects are marked as Default. Only one can be default.");
            }

            // Log information about loaded projects
            Log.Information("Loaded {Count} Bitbucket project configurations.", bitbucketProjects.Count);
            foreach (var proj in bitbucketProjects)
            {
                Log.Information(" - Repo Slug: {RepoSlug}, Account: {AccountName}, Default: {IsDefault}",
                    proj.RepoSlug, proj.AccountName, proj.Default);
            }

            // Add Serilog
            builder.Services.AddSerilog();

            // Add the configurations and the factory to the DI container
            builder.Services.AddSingleton(bitbucketProjects);
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
            app.MapMcp("{repo_slug}");

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
