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
        // Validate that repo_slug is provided in the route
        if (string.IsNullOrEmpty(repoSlug))
        {
            var errorMessage = "Repository slug must cannot be empty";
            _logger.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }

        _logger.LogDebug("Creating Bitbucket client for Repo Slug: {RepoSlug}", repoSlug);
        return await CreateClientAsync(repoSlug, scopeDescription: $"Repo: {repoSlug}");
    }

    [RequiresUnreferencedCode("Uses reflection")]
    public async Task<Result<BitbucketClient>> CreateWorkspaceClientAsync()
    {
        _logger.LogDebug("Creating workspace-scoped Bitbucket client for Account: {AccountName}", _projectConfig.AccountName);
        return await CreateClientAsync(repoSlug: null, scopeDescription: $"Workspace: {_projectConfig.AccountName}");
    }

    [RequiresUnreferencedCode("Uses reflection")]
    private async Task<Result<BitbucketClient>> CreateClientAsync(string? repoSlug, string scopeDescription)
    {
        // SharedCredentialResolver (the default, Broker disabled) always returns null here, so
        // this is a no-op fall-through to the shared service credential below — byte-identical to
        // today's behavior. BrokerCredentialResolver (Broker enabled) resolves the calling user's
        // own Bitbucket access token from their validated JWT instead.
        var credentialResult = await _credentialResolver.ResolveAsync(_httpContextAccessor?.HttpContext?.User);
        if (credentialResult.IsFailed)
        {
            var credentialError = credentialResult.Errors.FirstOrDefault()?.Message ?? "Unknown error";
            _logger.LogWarning(
                "Failed to resolve upstream Bitbucket credential for {ScopeDescription}. Error: {ErrorMessage}",
                scopeDescription, credentialError);
            return Result.Fail(credentialError);
        }

        var accessToken = credentialResult.Value;

        // Use credentials that were resolved from environment variables at boot time in Program.cs
        _logger.LogDebug("Creating Bitbucket client for Account: {AccountName}, {ScopeDescription}, User: {Username}",
            _projectConfig.AccountName, scopeDescription, _projectConfig.Username);

        var client = new BitbucketClient(_projectConfig.AccountName,
                                         repoSlug,
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
            _logger.LogError("Failed to create Bitbucket client for Account: {AccountName}, {ScopeDescription}. Error: {ErrorMessage}",
                _projectConfig.AccountName, scopeDescription, errorMessage);
            return Result.Fail($"Failed to create Bitbucket client for {scopeDescription}: {errorMessage}");
        }

        _logger.LogDebug("Successfully created Bitbucket client for Account: {AccountName}, {ScopeDescription}, FullName: {RepositoryFullName}",
            _projectConfig.AccountName, scopeDescription, client.RepositoryFullName);

        return Result.Ok(client);
    }
}
