namespace BitbucketMcpTools;

public partial class PullRequestTools
{
    private readonly IServiceProvider _serviceProvider;

    public PullRequestTools(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }
}
