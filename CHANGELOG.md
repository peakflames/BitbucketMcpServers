# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.0.2] - 2025-12-04

### Changed
- Environment variables are now resolved once at startup in `Program.cs` instead of at runtime
- Simplified `BitbucketRemoteClientFactory` by removing redundant `ResolveEnvironmentVariable` method
- Updated `appsettings.json` to only contain `AccountName` (credentials come from environment variables)
- Updated README files to remove `OBTAIN_FROM_ENV_VAR` placeholder references
- Improved documentation to clarify that credentials are validated at boot time

### Fixed
- Fixed analyzer hints (Substring simplification, LogError template variation)

### Removed
- Removed `ResolveEnvironmentVariable` method from `BitbucketRemoteClientFactory`
- Removed `Username` and `AppPassword` fields from `appsettings.json`

## [0.0.1] - 2025-11-06

### Added
- Initial release of Bitbucket MCP Servers
- Support for Bitbucket Cloud integration via MCP (Model Context Protocol)
- `list_pull_open_requests` tool to get all open pull requests in a repository
- `get_pull_request_comments` tool to get comments for a specific pull request
- BitbucketRemoteMcpServer (ASP.NET Web API-based) with SSE and Streamable HTTP transport
- BitbucketMcpServer (Console-based) with stdio transport
- Docker support with built-in .NET SDK container capabilities
- Environment variable support for secure credential management

