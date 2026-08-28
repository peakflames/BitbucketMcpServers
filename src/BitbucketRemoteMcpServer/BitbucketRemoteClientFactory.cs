using System.Diagnostics.CodeAnalysis;
using BitbucketMcpTools;
using BitbucketRemoteMcpServer.Credentials;
using FluentResults;

namespace BitbucketRemoteMcpServer;

public class BitbucketRemoteClientFactory : IBitbucketClientFactory
{
    private readonly BitbucketProjectConfig _projectConfig;
    private readonly ILogger<BitbucketRemoteClientFactory> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IUpstreamCredentialResolver _credentialResolver;
    private readonly string? _baseUrl;

    public BitbucketRemoteClientFactory(
        BitbucketProjectConfig projectConfig,
        ILogger<BitbucketRemoteClientFactory> logger,
        IHttpContextAccessor? httpContextAccessor,
        IUpstreamCredentialResolver credentialResolver,
        string? baseUrl = null)
    {
        _projectConfig = projectConfig;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _credentialResolver = credentialResolver;
        // baseUrl is a test-only seam (points BitbucketClient at a FakeBitbucketServer); no
        // service is registered for a bare `string?` so DI supplies the default (null) in
        // production, identical to BitbucketClient's own baseUrl parameter.
        _baseUrl = baseUrl;
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

        // SharedCredentialResolver (the default, Broker disabled) always returns null here, so
        // this is a no-op fall-through to the shared service credential below — byte-identical to
        // today's behavior. BrokerCredentialResolver (Broker enabled) resolves the calling user's
        // own Bitbucket access token from their validated JWT instead.
        var credentialResult = await _credentialResolver.ResolveAsync(_httpContextAccessor?.HttpContext?.User);
        if (credentialResult.IsFailed)
        {
            var credentialError = credentialResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            _logger.LogWarning(
                "Failed to resolve upstream Bitbucket credential for Repo: {RepoSlug}. Error: {ErrorMessage}",
                routeRepoSlug, credentialError);
            return Result.Fail(credentialError);
        }

        var accessToken = credentialResult.Value;

        // Use credentials that were resolved from environment variables at boot time in Program.cs
        _logger.LogDebug("Creating Bitbucket client for Account: {AccountName}, Repo: {RepoSlug}, User: {Username}",
            _projectConfig.AccountName, routeRepoSlug, _projectConfig.Username);

        // Create the BitbucketClient using the resolved values and route-provided repo slug
        var client = new BitbucketClient(_projectConfig.AccountName,
                                         routeRepoSlug,
                                         _projectConfig.Username,
                                         _projectConfig.AppPassword,
                                         _projectConfig.ConsumerKey,
                                         _projectConfig.SecretKey,
                                         accessToken,
                                         _baseUrl);

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
}
