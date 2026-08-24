namespace BitbucketRemoteMcpServer.Tests;

/// <summary>
/// Reflects over every [McpServerTool] in BitbucketMcpTools and fails if a tool isn't classified
/// as repo-scoped or workspace-scoped. That classification is what decides whether a future
/// per-user-credential resolver needs a single named repo (repo-scoped; a 404 on an inaccessible
/// repo is enough) or workspace-wide access (workspace-scoped; needs a workspace-level client
/// factory method, landing in a later phase). Reflection is fine here — it must never be used in
/// shipping code.
/// </summary>
public class ToolInventoryTests
{
    private static readonly HashSet<string> RepoScopedTools = new(StringComparer.Ordinal)
    {
        "read_file",
        "list_directory",
        "list_branches",
        "list_commits",
        "get_commit",
        "list_pull_open_requests",
        "list_pull_requests",
        "get_pull_request_comments",
        "get_pull_request_details",
    };

    private static readonly HashSet<string> WorkspaceScopedTools = new(StringComparer.Ordinal)
    {
        "list_repositories",
        "search_code",
    };

    [Fact]
    public void EveryMcpServerTool_IsClassifiedAsRepoOrWorkspaceScoped()
    {
        var discovered = DiscoverToolNames();

        Assert.NotEmpty(discovered);

        var expected = new HashSet<string>(RepoScopedTools, StringComparer.Ordinal);
        expected.UnionWith(WorkspaceScopedTools);

        var unclassified = discovered.Where(n => !expected.Contains(n)).ToArray();
        Assert.True(
            unclassified.Length == 0,
            $"Unclassified tool(s) found: {string.Join(", ", unclassified)}. " +
            "Add each to RepoScopedTools or WorkspaceScopedTools in ToolInventoryTests.");

        var missing = expected.Where(n => !discovered.Contains(n)).ToArray();
        Assert.True(
            missing.Length == 0,
            $"Tool(s) expected but not found: {string.Join(", ", missing)}. " +
            "A tool was removed or renamed without updating ToolInventoryTests.");
    }

    private static string[] DiscoverToolNames() =>
        typeof(RepositoryTools).Assembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Select(m => m.GetCustomAttribute<McpServerToolAttribute>())
            .Where(a => a is not null)
            .Select(a => a!.Name!)
            .ToArray();
}
