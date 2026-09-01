# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-09-01

### Added
- **`McpAuth`** config section (default off): when enabled, `POST /mcp` requires an RS256 bearer
  token from an external OIDC authorization server — issuer, audience, lifetime, and signing key
  are all validated — **and** a `bitbucket:read` scope claim (`scope` or `scp`). Unauthenticated
  requests get a 401 carrying an RFC 9728 `resource_metadata` challenge, served from
  `/.well-known/oauth-protected-resource/mcp`. This server never issues these tokens itself.
- **`Broker`** config section (default off; requires `McpAuth:Enabled`, or the server refuses to
  start): the server becomes its own OAuth 2.1 authorization server that delegates sign-in to
  Bitbucket, adding `/authorize`, `/oauth/callback`, `/token`, `/register` (RFC 7591 dynamic client
  registration, itself off by default behind `Broker:DcrEnabled`),
  `/.well-known/oauth-authorization-server`, and `/.well-known/jwks.json`. Every tool call then
  resolves the *caller's own* Bitbucket token from a SQLite-backed store instead of one shared
  identity, so two authenticated callers see results scoped to their own real Bitbucket
  permissions. A caller with no live token, or whose refresh was rejected, gets a "reconnect and
  re-authenticate" error rather than a silent fallback to the shared credential. Configuration,
  secret handling, and the required database volume mount:
  [README.md → Per-user OAuth (Broker)](README.md#per-user-oauth-broker).
- `--self-test-broker-storage <path>` command-line mode on `BitbucketRemoteMcpServer`, which
  exercises the broker's SQLite store and exits without starting the host
- A test suite under `tests/`, run with `python build.py test`

### Changed
- **BREAKING:** `list_repositories` and `search_code` no longer take a `repoName` parameter, on
  both the stdio and remote servers. It existed only to bootstrap a credential against an arbitrary
  named repository for what are workspace-wide operations; the credential is now validated against
  the workspace itself (`GET /workspaces/{workspace}`). Implementers of `IBitbucketClientFactory`
  must add `CreateWorkspaceClientAsync()`.
- `ModelContextProtocol`/`ModelContextProtocol.AspNetCore` 0.7.0-preview.1 → 2.1.0 (all three
  projects)
- `SharpBucket` 0.17.0 → `Peakflames.SharpBucket` 0.18.0 — a fork that accepts an externally
  obtained bearer token, which upstream cannot (it can only run its own client-credentials grant).
  No API changes for this project's usage.
- MCP HTTP transport is now stateless — no `Mcp-Session-Id` is issued
- The shared Bitbucket credential (`BITBUCKET_MCP_USERNAME`/`BITBUCKET_MCP_API_TOKEN`, or the OAuth
  2.0 client-credentials pair) is no longer required at boot when `Broker:Enabled` is true.
  Deployments not using the broker are unaffected — a shared credential is still required.

### Removed
- **BREAKING:** the `/sse`, `/message`, and root `/` MCP mounts. `/mcp` (Streamable HTTP) is the
  only endpoint — update any client still pointed at the old URLs. `python build.py mcp *` now
  connects to `/mcp`
- The fake-SSE GET-request workaround middleware for the Cline/TypeScript MCP SDK bug, unnecessary
  under the stateless transport
- A dead `Polarion` package reference and stale `ReverseMarkdown`/`HtmlAgilityPack` trimmer roots
  in `BitbucketRemoteMcpServer.csproj`

### Documentation
- Rewrote the root `README.md`: all 11 MCP tools (previously 4), plus an `## Authentication`
  section covering both the shared-credential and per-user Broker paths
- Deleted the stale, unlinked `src/BitbucketRemoteMcpServer/README.md`, folding its accurate
  `Broker`/`McpAuth` config tables into the root README; refreshed `CONTRIBUTING.md` and `CLAUDE.md`

## [0.1.3] - 2026-07-10

### Added
- `list_pull_requests` tool to retrieve pull requests in any state (Open, Merged, Declined, Superseded),
  with optional state filtering and a result cap
- Draft status surfaced in `list_pull_open_requests`, `list_pull_requests` (as a `Draft` column), and
  `get_pull_request_details` (as `is_draft` in `<PR_METADATA>`). Fetched via a supplemental raw-JSON
  request since the SharpBucket `PullRequest` POCO does not expose the `draft` field; degrades to
  `?`/`unknown` if the lookup fails
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
