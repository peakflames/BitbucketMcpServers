# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCP (Model Context Protocol) servers for interacting with Bitbucket Cloud. Provides two deployment options:
- **BitbucketMcpServer**: Console app using stdio transport for local MCP clients (Cline, etc.)
- **BitbucketRemoteMcpServer**: ASP.NET Web API using HTTP transport for remote/containerized deployment

## Build Commands

Use the Python build automation script for all build and run operations:

```bash
python build.py build    # Build solution (auto-stops running app)
python build.py start    # Build and start in background (port 5107)
python build.py stop     # Stop background application
python build.py status   # Check if application is running
python build.py run      # Run in foreground (blocks terminal)
```

### MCP Commands
```bash
python build.py mcp ping                              # Check MCP server connectivity
python build.py mcp info                              # Show MCP server information
python build.py mcp tools                             # List available MCP tools
python build.py mcp call <tool> '{"arg": "value"}'    # Call an MCP tool with JSON args
```

### Log Commands
```bash
python build.py log                      # Show last 50 lines
python build.py log <pattern>            # Search for regex pattern
python build.py log --tail <n>           # Show last n lines
python build.py log --level error        # Filter by level (error/warn/info/debug)
```

### URLs (when running)
- http://localhost:5107 - Landing page
- http://localhost:5107/sse - MCP SSE endpoint

### Prerequisites
```bash
pip install psutil fastmcp
```

### Alternative dotnet commands
```bash
dotnet build BitbucketMcpServers.sln
dotnet publish src/BitbucketMcpServer/BitbucketMcpServer.csproj -o publish
dotnet run --project src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj
dotnet publish src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj /t:PublishContainer -r linux-x64
```

## Architecture

```
src/
├── BitbucketMcpServer/       # Console app (stdio MCP transport)
├── BitbucketRemoteMcpServer/ # ASP.NET app (HTTP MCP transport)
└── BitbucketMcpTools/        # Shared library with MCP tools and Bitbucket client
```

### Key Patterns

**MCP Tools**: Defined in `BitbucketMcpTools/PullRequestTools*.cs` as partial classes. Each MCP tool is a method with `[McpServerTool]` attribute. Add new tools in separate partial class files following the naming convention `PullRequestTools_<ToolName>.cs`.

**Client Factory Pattern**: `IBitbucketClientFactory` creates `BitbucketClient` instances. Two implementations:
- `BitbucketClientFactory`: For stdio server, uses CLI args/env vars for single repo config
- `BitbucketRemoteClientFactory`: For HTTP server, resolves credentials from appsettings with `OBTAIN_FROM_ENV_VAR_` prefix pattern for sensitive values

**Configuration**:
- Stdio server: CLI args (`-u`, `-p`, `-a`, `-r`) or env vars (`BITBUCKET_USERNAME`, `BITBUCKET_APP_PASSWORD`, `BITBUCKET_ACCOUNT_NAME`, `BITBUCKET_REPO_SLUG`)
- Remote server: `appsettings.json` with `BitbucketCloudConfig` section; env vars `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN`

### Dependencies

- **SharpBucket**: Bitbucket Cloud API client
- **FluentResults**: Result pattern for error handling
- **ModelContextProtocol**: MCP SDK (.NET)
- **Serilog**: Logging

## Adding New MCP Tools

1. Create a new file: `src/BitbucketMcpTools/PullRequestTools_YourToolName.cs`
2. Place using statements in `GlobalUsings.cs`, not in individual files
3. Use this template:

```csharp
namespace BitbucketMcpTools;

public partial class PullRequestTools
{
    [McpServerTool(Name = "your_tool_name"),
     Description("Description of your tool")]
    public async Task<string> YourToolName(
        [Description("Description of parameter")] string parameterName)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var clientFactory = scope.ServiceProvider.GetRequiredService<IBitbucketClientFactory>();
        var clientResult = await clientFactory.CreateClientAsync(repoName);

        if (clientResult.IsFailed)
            return clientResult.Errors.First().ToString() ?? "Internal Error";

        var bitBucketClient = clientResult.Value;
        if (bitBucketClient is null)
            return "Internal Error unknown error when creating Bitbucket client";

        try
        {
            // Implement your tool logic here
            return "Your result in markdown format";
        }
        catch (Exception ex)
        {
            return $"ERROR: Failed due to exception '{ex.Message}'";
        }
    }
}
```

## C# Coding Conventions

- Use `var` for all variables
- Use curly braces for all blocks
- Prefer Global Using Statements over Local Using Statements (add to `GlobalUsings.cs`)
- Prefer FluentResults over null handling or Exceptions for error handling
- Return error messages with "ERROR:" prefix
- Return results in markdown format

### MCP Tool Attribute Syntax
Use multi-line format - Description must be a separate attribute:
```csharp
[McpServerTool(Name = "tool_name"),
 Description("Tool description")]
```

## Verification Before Commit

A successful build does NOT equal working code. The workflow should be:

1. Implement changes
2. Build: `python build.py build`
3. Start: `python build.py start`
4. Verify: Use MCP tools or manual testing
5. Commit only after verification

## Git Workflow

- Prefer `--no-ff` when merging to preserve commit history
- Use explicit file paths in `git add` commands rather than wildcards

## CRITICAL: appsettings.json Security

**NEVER commit `src/BitbucketRemoteMcpServer/appsettings*.json`** - it contains sensitive credentials.

- Never use `git add` on this file
- Never stage, reset, or checkout this file
- Use explicit file paths in git commands to avoid accidentally including it

## Debugging

Debug the streamable HTTP server using MCP Inspector:
1. Start `BitbucketRemoteMcpServer`
2. Run `npx @modelcontextprotocol/inspector`
3. Connect with TransportType: streamable http, URL: http://localhost:5107/

## Version Management

Update version and container tag in the csproj file's `Version` and `ContainerImageTag` properties before publishing.

## Release Protocol

When the user requests "perform a release":

1. **Update CHANGELOG.md** - Change "Unreleased" to today's date, ensure all changes documented
2. **Commit and push develop** - Add explicit files, commit with "Release version X.Y.Z" message
3. **Merge to main** - `git checkout main && git pull && git merge develop --no-ff` with merge commit message, push
4. **Tag and push** - `git tag -a vX.Y.Z -m "Release version X.Y.Z"`, push tag
5. **Prepare next version** - Switch to develop, bump versions in both csproj files (Version and ContainerImageTag), add "Unreleased" section to CHANGELOG.md, commit "prepare for next development cycle (X.Y.Z+1)", push

Important: Use `--no-ff` for merges, explicit file paths in `git add`, never commit appsettings*.json
