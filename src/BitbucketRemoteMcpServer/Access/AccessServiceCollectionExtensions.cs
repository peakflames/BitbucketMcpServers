namespace BitbucketRemoteMcpServer.Access;

/// <summary>
/// Deliberate copy of the shape of Auth's AddMcpAuth: reads Access:Enabled eagerly and returns
/// false before registering anything — no options bind, no ValidateOnStart, no filters. Same
/// rationale: unconditional ValidateOnStart would let a malformed Access section break servers
/// with this feature off.
/// </summary>
public static class AccessServiceCollectionExtensions
{
    public static bool AddAccess(this WebApplicationBuilder builder)
    {
        var enabled = builder.Configuration.GetValue($"{AccessOptions.SectionName}:Enabled", false);
        if (!enabled)
            return false;

        var services = builder.Services;

        services.AddOptions<AccessOptions>()
            .Bind(builder.Configuration.GetSection(AccessOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AccessOptions>, AccessOptionsValidator>();

        services.Configure<McpServerOptions>(o =>
        {
            o.Filters.Request.ListToolsFilters.Add(DisabledToolsFilter.CreateListFilter);
            o.Filters.Request.CallToolFilters.Add(DisabledToolsFilter.CreateCallFilter);
        });

        return true;
    }
}
