using ModelContextProtocol.Protocol;

namespace BitbucketRemoteMcpServer.Access;

/// <summary>
/// Hides and denies the tools listed in Access:DisabledTools. Registered via
/// <c>services.Configure&lt;McpServerOptions&gt;(o =&gt; { o.Filters.Request.ListToolsFilters.Add(...); ... })</c>.
///
/// Deliberately not a permission decision and deliberately not named Rbac: it makes no
/// per-caller judgment, it just removes a small, explicitly-named set of tools for everyone
/// during the transitional window between authentication landing and per-user credential
/// passthrough landing in a later phase.
///
/// Returns a CallToolResult directly rather than throwing McpException: the SDK's own "no such
/// tool" check runs before the filter pipeline and answers with a JSON-RPC protocol-level error
/// (code -32602), which a CallToolFilter has no way to produce — filters only ever see tools that
/// *are* registered, so their output is always wrapped as a result, never a protocol error. A
/// disabled tool therefore cannot be made byte-identical on the wire to a tool that was never
/// registered; the message text below only avoids naming the real reason (an access policy), the
/// same "not this" bar as an upstream 404 rather than true wire-level parity. A future per-user
/// credential gate carries the same constraint and the same bar.
/// </summary>
public static class DisabledToolsFilter
{
    public static McpRequestHandler<ListToolsRequestParams, ListToolsResult> CreateListFilter(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next)
    {
        return async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken);

            var services = request.Services
                ?? throw new InvalidOperationException("DisabledToolsFilter requires RequestContext.Services.");
            var options = services.GetRequiredService<IOptions<AccessOptions>>().Value;

            if (options.AuditOnly || options.DisabledTools.Count == 0)
                return result;

            result.Tools = result.Tools
                .Where(tool => !options.DisabledTools.Contains(tool.Name, StringComparer.Ordinal))
                .ToList();

            return result;
        };
    }

    public static McpRequestHandler<CallToolRequestParams, CallToolResult> CreateCallFilter(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next)
    {
        return async (request, cancellationToken) =>
        {
            var services = request.Services
                ?? throw new InvalidOperationException("DisabledToolsFilter requires RequestContext.Services.");
            var options = services.GetRequiredService<IOptions<AccessOptions>>().Value;

            var toolName = request.Params?.Name ?? string.Empty;
            var disabled = options.DisabledTools.Contains(toolName, StringComparer.Ordinal);

            if (disabled && !options.AuditOnly)
            {
                return new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = $"ERROR: Unknown tool: '{toolName}'" }],
                };
            }

            return await next(request, cancellationToken);
        };
    }
}
