using System.Diagnostics.CodeAnalysis;
using BitbucketMcpTools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SharpBucket.V2.Pocos;


namespace PolarionMcpServer;

[RequiresUnreferencedCode("Uses reflection")]
public class Program
{
    [RequiresUnreferencedCode("Uses reflection")]
    public static int Main(string[] args)
    {
        try
        {
            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Warning()
                            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "BitbucketMcpServer_.log"),
                                rollingInterval: RollingInterval.Day,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                            .WriteTo.Debug()
                            .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                            .CreateLogger();

            Log.Information("Bitbucket MCP Server starting...");
            // Log.Information("Attempting to connect to Bitbucket and retrieve pull requests based on configuration...");

            string? GetConfigValue(string[] cliArgs, string cliParamShort, string cliParamLong, string envVarName, bool isSensitive = false)
            {
                for (int i = 0; i < cliArgs.Length; i++)
                {
                    if ((string.Equals(cliArgs[i], cliParamShort, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cliArgs[i], cliParamLong, StringComparison.OrdinalIgnoreCase)) && i + 1 < cliArgs.Length)
                    {
                        string valueToLog = isSensitive ? "****" : cliArgs[i + 1];
                        // Log.Debug($"Found CLI parameter {cliArgs[i]} with value {valueToLog}");
                        return cliArgs[i + 1];
                    }
                }
                string? envValue = Environment.GetEnvironmentVariable(envVarName);
                if (!string.IsNullOrEmpty(envValue))
                {
                    string valueToLog = isSensitive ? "****" : envValue;
                    // Log.Debug($"Found environment variable {envVarName} with value {valueToLog}");
                }
                return envValue;
            }

            string? bitbucketUsername = GetConfigValue(args, "-u", "--username", "BITBUCKET_USERNAME");
            string? bitbucketAppPassword = GetConfigValue(args, "-p", "--password", "BITBUCKET_APP_PASSWORD", isSensitive: true);
            string? accountName = GetConfigValue(args, "-a", "--account", "BITBUCKET_ACCOUNT_NAME");
            string? repoSlug = GetConfigValue(args, "-r", "--repo", "BITBUCKET_REPO_SLUG");

            bool configMissing = false;
            if (string.IsNullOrEmpty(bitbucketUsername)) { Log.Error("Bitbucket Username not provided. Use -u/--username <value> or set BITBUCKET_USERNAME environment variable."); configMissing = true; }
            if (string.IsNullOrEmpty(bitbucketAppPassword)) { Log.Error("Bitbucket App Password not provided. Use -p/--password <value> or set BITBUCKET_APP_PASSWORD environment variable."); configMissing = true; }
            if (string.IsNullOrEmpty(accountName)) { Log.Error("Bitbucket Account Name not provided. Use -a/--account <value> or set BITBUCKET_ACCOUNT_NAME environment variable."); configMissing = true; }
            if (string.IsNullOrEmpty(repoSlug)) { Log.Error("Bitbucket Repo Slug not provided. Use -r/--repo <value> or set BITBUCKET_REPO_SLUG environment variable."); configMissing = true; }

            if (configMissing)
            {
                Log.Error("Skipping Bitbucket API interaction due to missing configuration. Please provide all required parameters.");
                return 1;
            }

            // Create the DI container
            //
            var builder = Host.CreateApplicationBuilder(args);

            // Add Serilog
            //
            builder.Services.AddSerilog();


            // Add the BitBucketConfig and IBitbucketClientFactory to the DI container
            //
            builder.Services.AddSingleton(new BitBucketConfig(bitbucketUsername!, bitbucketAppPassword!, accountName!, repoSlug!));
            builder.Services.AddScoped<IBitbucketClientFactory, BitbucketClientFactory>();

            // Add the McpServer to the DI container
            //
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithTools<BitbucketMcpTools.PullRequestTools>();

            // Build and Run the McpServer
            //
            Log.Information("Starting BitBucketMcpServer...");
            builder.Build().Run();
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
