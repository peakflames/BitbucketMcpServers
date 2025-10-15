using System.Diagnostics.CodeAnalysis;
using BitbucketMcpTools;
using FluentResults;

namespace BitbucketRemoteMcpServer;

public class BitbucketRemoteClientFactory : IBitbucketClientFactory
{
    private readonly List<BitbucketProjectConfig> _projectConfigs;
    private readonly ILogger<BitbucketRemoteClientFactory> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public BitbucketRemoteClientFactory(
        List<BitbucketProjectConfig> projectConfigs,
        ILogger<BitbucketRemoteClientFactory> logger,
        IHttpContextAccessor? httpContextAccessor)
    {
        _projectConfigs = projectConfigs;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    // Public property to get the repo_slug from route data
    public string? RepoSlug => _httpContextAccessor?.HttpContext?.GetRouteValue("repo_slug")?.ToString();

    [RequiresUnreferencedCode("Uses reflection")]
    public async Task<Result<BitbucketClient>> CreateClientAsync()
    {
        string? routeRepoSlug = RepoSlug;
        _logger.LogDebug("Attempting to create Bitbucket client for requested Repo Slug: {RouteRepoSlug}", routeRepoSlug ?? "[Not Provided]");

        BitbucketProjectConfig? selectedConfig = null;

        // Try to find a configuration matching the route repo_slug (case-insensitive)
        if (!string.IsNullOrEmpty(routeRepoSlug))
        {
            selectedConfig = _projectConfigs.FirstOrDefault(p =>
                p.RepoSlug.Equals(routeRepoSlug, StringComparison.OrdinalIgnoreCase));

            if (selectedConfig != null)
            {
                _logger.LogDebug("Found matching configuration for Repo Slug: {RepoSlug}", selectedConfig.RepoSlug);
            }
        }

        // If no specific match found, try to find the default configuration
        if (selectedConfig == null)
        {
            selectedConfig = _projectConfigs.FirstOrDefault(p => p.Default);
            if (selectedConfig != null)
            {
                _logger.LogDebug("Using default configuration for Repo Slug: {RepoSlug}", selectedConfig.RepoSlug);
            }
            else
            {
                // If still no config (neither specific nor default), throw an error
                var errorMessage = $"Configuration error: No specific or default Bitbucket project configuration found for requested repo slug '{routeRepoSlug ?? "[Not Provided]"}'. Check appsettings.json.";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
        }

        // Resolve environment variables for sensitive data
        string username = ResolveEnvironmentVariable(selectedConfig.Username, "Username");
        string appPassword = ResolveEnvironmentVariable(selectedConfig.AppPassword, "AppPassword");

        _logger.LogDebug("Creating Bitbucket client for Account: {AccountName}, Repo: {RepoSlug}, User: {Username}",
            selectedConfig.AccountName, selectedConfig.RepoSlug, username);

        // Create the BitbucketClient using the resolved values
        var client = new BitbucketClient(username, appPassword, selectedConfig.AccountName, selectedConfig.RepoSlug);
        
        // Connect to Bitbucket
        var result = await client.ConnectAsync();
        if (result.IsFailed)
        {
            var errorMessage = result.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            _logger.LogError("Failed to create Bitbucket client for Account: {AccountName}, Repo: {RepoSlug}. Error: {ErrorMessage}",
                selectedConfig.AccountName, selectedConfig.RepoSlug, errorMessage);
            return Result.Fail($"Failed to create Bitbucket client for repo '{selectedConfig.RepoSlug}': {errorMessage}");
        }
        
        _logger.LogDebug("Successfully created Bitbucket client for Account: {AccountName}, Repo: {RepoSlug}, FullName: {RepositoryFullName}",
            selectedConfig.AccountName, selectedConfig.RepoSlug, client.RepositoryFullName);

        return Result.Ok(client);
    }

    private string ResolveEnvironmentVariable(string value, string fieldName)
    {
        const string envVarPrefix = "OBTAIN_FROM_ENV_VAR_";
        
        if (value.StartsWith(envVarPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string envVarName = value.Substring(envVarPrefix.Length);
            string? envValue = Environment.GetEnvironmentVariable(envVarName);
            
            if (string.IsNullOrEmpty(envValue))
            {
                var errorMessage = $"Environment variable '{envVarName}' for {fieldName} is not set or is empty.";
                _logger.LogError(errorMessage);
                throw new InvalidOperationException(errorMessage);
            }
            
            _logger.LogDebug("Resolved {FieldName} from environment variable: {EnvVarName}", fieldName, envVarName);
            return envValue;
        }
        
        // If not an environment variable placeholder, return the value as-is
        return value;
    }
}
