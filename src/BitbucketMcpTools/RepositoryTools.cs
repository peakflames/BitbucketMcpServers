namespace BitbucketMcpTools;

public partial class RepositoryTools
{
    private readonly IServiceProvider _serviceProvider;

    public RepositoryTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
}
