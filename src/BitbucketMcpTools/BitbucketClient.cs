using SharpBucket.V2.EndPoints;

namespace BitbucketMcpTools;

public class BitbucketClient(string accountName,
                             string repoSlug,
                             string? bitbucketUsername,
                             string? bitbucketAppPassword,
                             string? bitbucektConsumerKey,
                             string? bitbucketSecretKey,
                             string? accessToken = null,
                             string? baseUrl = null)
{
    private readonly string? _bitbucketUsername = bitbucketUsername;
    private readonly string? _bitbucketAppPassword = bitbucketAppPassword;
    private readonly string? _bitbucektConsumerKey = bitbucektConsumerKey;
    private readonly string? _bitbucketSecretKey = bitbucketSecretKey;
    private readonly string? _accessToken = accessToken;
    private readonly string _accountName = accountName;
    private readonly string _repoSlug = repoSlug;
    private readonly string? _baseUrl = baseUrl;
    private RepositoryResource? _repositoryResource;
    private Repository? _repository;
    private string? _repositoryFullName;
    private SharpBucketV2? _sharpBucket;

    [RequiresUnreferencedCode("Uses reflection")]
    public async Task<Result> ConnectAsync()
    {
        // baseUrl is a test-only seam (points SharpBucketV2 at a local fake server); production
        // callers never pass it, so this is byte-identical to `new SharpBucketV2()` in prod.
        _sharpBucket = _baseUrl is null ? new SharpBucketV2() : new SharpBucketV2(_baseUrl);

        // An access token identifies a specific caller, so it wins over the shared service
        // credentials whenever one was supplied.
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            _sharpBucket.OAuth2BearerToken(_accessToken);
        }
        else if (!string.IsNullOrWhiteSpace(_bitbucektConsumerKey) && !string.IsNullOrWhiteSpace(_bitbucketSecretKey))
        {
            _sharpBucket.OAuth2ClientCredentials(_bitbucektConsumerKey, _bitbucketSecretKey);
        }
        else
        {
            _sharpBucket.BasicAuthentication(_bitbucketUsername!, _bitbucketAppPassword!);
        }

        // Validate authentication by fetching repositories
        var repositoriesEndPoint = _sharpBucket.RepositoriesEndPoint();
        _repositoryResource = repositoriesEndPoint.RepositoryResource(_accountName!, _repoSlug!);

        // Validate repository access by fetching repository details
        try
        {
            _repository = await _repositoryResource.GetRepositoryAsync();
            _repositoryFullName = _repository.full_name;
        }
        catch (Exception ex)
        {
            return Result.Fail($"Unable to access repository: {_accountName}/{_repoSlug}. Error: {ex.Message}");
        }

        return Result.Ok();
    }


    public RepositoryResource? RepositoryResource => _repositoryResource;
    public Repository? Repository => _repository;
    public string? RepositoryFullName => _repositoryFullName;
    public string AccountName => _accountName;
    public SharpBucketV2? SharpBucket => _sharpBucket;
}