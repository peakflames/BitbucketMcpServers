using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Serilog;
using SharpBucket.V2;
using SharpBucket.V2.Pocos;


namespace PolarionMcpServer;

[RequiresUnreferencedCode("Uses reflection")]
public class Program
{
    [RequiresUnreferencedCode("Uses reflection")]
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Verbose() // Capture all log levels
                            .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "BitbucketMcpServer_.log"),
                                rollingInterval: RollingInterval.Day,
                                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                            .WriteTo.Debug()
                            .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
                            .CreateLogger();

            Log.Information("Bitbucket MCP Server starting...");
            Log.Information("Attempting to connect to Bitbucket and retrieve pull requests based on configuration...");

            string? GetConfigValue(string[] cliArgs, string cliParamShort, string cliParamLong, string envVarName, bool isSensitive = false)
            {
                for (int i = 0; i < cliArgs.Length; i++)
                {
                    if ((string.Equals(cliArgs[i], cliParamShort, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(cliArgs[i], cliParamLong, StringComparison.OrdinalIgnoreCase)) && i + 1 < cliArgs.Length)
                    {
                        string valueToLog = isSensitive ? "****" : cliArgs[i + 1];
                        Log.Debug($"Found CLI parameter {cliArgs[i]} with value {valueToLog}");
                        return cliArgs[i + 1];
                    }
                }
                string? envValue = Environment.GetEnvironmentVariable(envVarName);
                if (!string.IsNullOrEmpty(envValue))
                {
                    string valueToLog = isSensitive ? "****" : envValue;
                    Log.Debug($"Found environment variable {envVarName} with value {valueToLog}");
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
                Log.Warning("Skipping Bitbucket API interaction due to missing configuration. Please provide all required parameters.");
            }
            else
            {
                try
                {
                    var sharpBucket = new SharpBucketV2();
                    // Null forgiveness operator used here because configMissing check ensures they are not null.
                    sharpBucket.BasicAuthentication(bitbucketUsername!, bitbucketAppPassword!);
                    
                    // Validate authentication by fetching repositories
                    Log.Information($"Attempting to access and verify repository: {accountName}/{repoSlug}");
                    var repositoriesEndPoint = sharpBucket.RepositoriesEndPoint();
                    var repositoryResource = repositoriesEndPoint.RepositoryResource(accountName!, repoSlug!);

                    // Validate repository access by fetching repository details

                    Repository? repositoryDetails = null;
                    try
                    {
                        _ = await repositoryResource.GetRepositoryAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Unable to access repository: {accountName}/{repoSlug}. Error: {ex.Message}");
                        return 1;
                    }

                    Log.Information($"Successfully accessed repository: {repositoryDetails?.full_name}");

                    Log.Information("Proceeding to list pull requests...");
                    // Corrected: Get PullRequestsResource first, then list pull requests
                    var pullRequestsResource = repositoryResource.PullRequestsResource();
                    List<PullRequest> pullRequests = pullRequestsResource.ListPullRequests();

                    if (pullRequests != null && pullRequests.Any())
                    {
                        Log.Information($"Found {pullRequests.Count} pull request(s) for {accountName}/{repoSlug}:");
                        foreach (var pr in pullRequests)
                        {
                            // Corrected property names to lowercase based on the provided SharpBucket.V2.Pocos.PullRequest definition
                            // Assuming User POCO (type of pr.author) has a 'display_name' property.
                            Log.Information($"  - ID: {pr.id}, Title: \"{pr.title}\", Author: {pr.author?.display_name}, State: {pr.state}, Created: {pr.created_on}, Updated: {pr.updated_on}");
                        }
                    }
                    else
                    {
                        Log.Information($"No pull requests found for {accountName}/{repoSlug} or unable to retrieve them.");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"An error occurred while interacting with Bitbucket: {ex.Message}");
                    Log.Debug($"Stack Trace: {ex.StackTrace}"); // More detailed error for debugging
                }
            }
            
            Log.Information("Bitbucket MCP Server finished processing.");
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
