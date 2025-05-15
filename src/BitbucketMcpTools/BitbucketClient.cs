using SharpBucket.V2.EndPoints;

namespace BitbucketMcpTools;

public class BitbucketClient(string bitbucketUsername, string bitbucketAppPassword, string accountName, string repoSlug)
{
    private readonly string _bitbucketUsername = bitbucketUsername;
    private readonly string _bitbucketAppPassword = bitbucketAppPassword;
    private readonly string _accountName = accountName;
    private readonly string _repoSlug = repoSlug;
    private RepositoryResource? _repositoryResource;
    private Repository? _repository;
    private string? _repositoryFullName;
    private SharpBucketV2? _sharpBucket;

    [RequiresUnreferencedCode("Uses reflection")]
    public async Task<Result> ConnectAsync()
    {
        _sharpBucket = new SharpBucketV2();
        // Null forgiveness operator used here because configMissing check ensures they are not null.
        _sharpBucket.BasicAuthentication(_bitbucketUsername!, _bitbucketAppPassword!);

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
}