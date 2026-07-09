# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.3] - Unreleased

### Added
- `list_pull_requests` tool to retrieve pull requests in any state (Open, Merged, Declined, Superseded),
  with optional state filtering and a result cap
- New `RepositoryTools` partial class with read-only repository browsing and code search tools:
  - `read_file` - reads a file's raw contents at a given revision, with a configurable size cap and truncation notice
  - `list_directory` - shallow listing of files/directories at a given revision
  - `search_code` - searches code content across all repositories in the workspace
  - `list_repositories` - lists repositories in the workspace, with optional name filtering
  - `list_branches` - lists branches in a repository, with optional name filtering
  - `list_commits` - lists commit history for a repository or branch
  - `get_commit` - gets details for a single commit by hash

### Changed
- Clarified `get_pull_request_details` and `get_pull_request_comments` descriptions to note they work
  for a pull request in any state, not just open ones
- `BitbucketClient` now exposes the underlying `SharpBucketV2` instance and account name so workspace-scoped
  tools (beyond the single configured repository) can be built

### Fixed

## [0.1.2] - 2025-02-06

### Changed
- Upgraded ModelContextProtocol SDK from 0.4.0-preview.2 to 0.7.0-preview.1
- Upgraded ModelContextProtocol.AspNetCore SDK from 0.4.0-preview.2 to 0.7.0-preview.1

### Added
- Forwarded headers middleware support for reverse proxy compatibility
- Streamable HTTP transport endpoint at `/mcp` for Cline compatibility
- SSE stream disconnection workaround for Cline/TypeScript MCP SDK
- Unicode output fix for Windows terminal in build.py script

### Fixed
- MCP endpoint routing for streamableHttp transport
- 404 errors when connecting Cline to /mcp endpoint

## [0.1.1] - 2025-01-16

### Added
- `get_pull_request_details` tool to retrieve pull request description, metadata, and changed files
- New `DiffstatModels.cs` for JSON deserialization of Bitbucket diffstat API responses
- Support for paginated diffstat retrieval to handle large pull requests

## [0.1.0] - 2025-12-05

### Documentation
- Added comprehensive OAuth 2.0 authentication documentation and setup guide
- Improved authentication flow documentation for better developer onboarding

## [0.0.4] - 2025-12-04

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

## [0.0.3] - 2025-10-18

### Changed
- Updated README with comprehensive setup and configuration guide

### Added
- Added `latest` tag and README sync to Docker publish workflow
- Enhanced documentation for easier onboarding

## [0.0.2] - 2025-10-16

### Added
- Docker image build and publish workflow via GitHub Actions
- Automated CI/CD pipeline for container publishing
- Support for automatic Docker Hub publishing on release

### Changed
- Removed manual latest tag push from docker workflow (automated by GitHub Actions)

## [0.0.1] - 2025-10-15

### Added
- Initial release of Bitbucket MCP Servers
- Support for Bitbucket Cloud integration via MCP (Model Context Protocol)
- `list_pull_open_requests` tool to get all open pull requests in a repository
- `get_pull_request_comments` tool to get comments for a specific pull request
- BitbucketRemoteMcpServer (ASP.NET Web API-based) with SSE and Streamable HTTP transport
- BitbucketMcpServer (Console-based) with stdio transport
- Docker support with built-in .NET SDK container capabilities
- Environment variable support for secure credential management
- Authentication validation early in the flow

### Changed
- Simplified Bitbucket configuration to single repository
- Split PullRequestTools into separate files per method for better organization

### Documentation
- Added build instructions
- Fixed container repository typo
- Initial README with basic setup information
