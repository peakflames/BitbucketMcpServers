using System.Diagnostics.CodeAnalysis;
using BitbucketMcpTools;
using FluentResults;

namespace BitbucketRemoteMcpServer;

public class BitbucketRemoteClientFactory : IBitbucketClientFactory
{
    private readonly BitbucketProjectConfig _projectConfig;
    private readonly ILogger<BitbucketRemoteClientFactory> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public BitbucketRemoteClientFactory(
        BitbucketProjectConfig projectConfig,
        ILogger<BitbucketRemoteClientFactory> logger,
        IHttpContextAccessor? httpContextAccessor)
    {
        _projectConfig = projectConfig;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    // Public property to get the repo_slug from route data
    public string? RepoSlug => _httpContextAccessor?.HttpContext?.GetRouteValue("repo_slug")?.ToString();

    [RequiresUnreferencedCode("Uses reflection")]
    public async Task<Result<BitbucketClient>> CreateClientAsync(string repoSlug)
    {
        string? routeRepoSlug = repoSlug;
        
        // Validate that repo_slug is provided in the route
        if (string.IsNullOrEmpty(routeRepoSlug))
        {
            var errorMessage = "Repository slug must cannot be empty";
            _logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogDebug("Creating Bitbucket client for Repo Slug: {RouteRepoSlug}", routeRepoSlug);

        // Resolve environment variables for sensitive data
        string username = ResolveEnvironmentVariable(_projectConfig.Username, "Username");
        string appPassword = ResolveEnvironmentVariable(_projectConfig.AppPassword, "AppPassword");

        _logger.LogDebug("Creating Bitbucket client for Account: {AccountName}, Repo: {RepoSlug}, User: {Username}",
            _projectConfig.AccountName, routeRepoSlug, username);

        // Create the BitbucketClient using the resolved values and route-provided repo slug
        var client = new BitbucketClient(username, appPassword, _projectConfig.AccountName, routeRepoSlug);
        
        // Connect to Bitbucket
        var result = await client.ConnectAsync();
        if (result.IsFailed)
        {
            var errorMessage = result.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            _logger.LogError("Failed to create Bitbucket client for Account: {AccountName}, Repo: {RepoSlug}. Error: {ErrorMessage}",
                _projectConfig.AccountName, routeRepoSlug, errorMessage);
            return Result.Fail($"Failed to create Bitbucket client for repo '{routeRepoSlug}': {errorMessage}");
        }
        
        _logger.LogDebug("Successfully created Bitbucket client for Account: {AccountName}, Repo: {RepoSlug}, FullName: {RepositoryFullName}",
            _projectConfig.AccountName, routeRepoSlug, client.RepositoryFullName);

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
