# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MCP (Model Context Protocol) servers for interacting with Bitbucket Cloud. Provides two deployment options:
- **BitbucketMcpServer**: Console app using stdio transport for local MCP clients (Cline, etc.)
- **BitbucketRemoteMcpServer**: ASP.NET Web API using HTTP transport for remote/containerized deployment

## Build Commands

```bash
# Build entire solution
dotnet build BitbucketMcpServers.sln

# Build standalone executable for local MCP
dotnet publish src/BitbucketMcpServer/BitbucketMcpServer.csproj -o publish

# Run the remote server locally
dotnet run --project src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj

# Build Docker image for remote server
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

## Debugging

Debug the streamable HTTP server using MCP Inspector:
1. Start `BitbucketRemoteMcpServer`
2. Run `npx @modelcontextprotocol/inspector`
3. Connect with TransportType: streamable http, URL: http://localhost:5107/

## Version Management

Update version and container tag in the csproj file's `Version` and `ContainerImageTag` properties before publishing.
