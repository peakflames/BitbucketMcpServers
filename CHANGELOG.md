# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - Unreleased

SDK upgrade, stateless transport, a testability seam, and an optional OAuth 2.1 resource server in
front of `/mcp` — disabled by default. No behavior change for existing callers who don't opt in,
other than the transport removal below.

### Added
- `McpAuth` config section: when `McpAuth:Enabled` is `true`, `POST /mcp` requires a bearer token
  (RS256, validated issuer/audience/lifetime/signing key) issued by an external authorization
  server, and the server answers unauthenticated requests with a 401 carrying an RFC 9728
  `resource_metadata` challenge plus the corresponding `/.well-known/oauth-protected-resource/mcp`
  document. The server never issues tokens itself — Okta (or any OIDC-compliant AS) is external.
- `Access` config section: `Access:DisabledTools` hides the named tools from `tools/list` and
  denies `tools/call` for them, for the transitional window where authentication exists but
  per-user credential passthrough (a later phase) does not yet. Requires `McpAuth:Enabled`.
- Test harness: `tests/BitbucketRemoteMcpServer.Tests`, `tests/StubAuthorizationServer` (mints
  JWTs in-test, no network/Okta dependency), and `tests/Fakes` (a real local Bitbucket-shaped HTTP
  server + a fake `IBitbucketClientFactory` for golden-output coverage, since SharpBucket has no
  in-memory HTTP injection point). Test coverage: `AuthDisabledRegressionTests`,
  `ChallengeAndDiscoveryTests`, `ResourceServerTokenValidationTests` (valid/expired/wrong-issuer/
  wrong-audience/unsigned/unknown-signing-key tokens), `AccessDisabledToolsTests`
- `python build.py test` to run the test suite
- `BitbucketClient` accepts an optional `baseUrl` constructor parameter (test-only seam; `null`
  in every production code path, so behavior is unchanged)
- `BitbucketRemoteMcpServer.Program` exposes `BuildApp(args, configure, postAuthConfigure)` so
  tests can drive the app via `UseTestServer()` instead of a real listener
- `BitbucketClient` accepts an optional `accessToken` constructor parameter. When supplied it is
  sent as an `Authorization: Bearer` header and takes precedence over the shared service
  credentials; when omitted, the existing client-credentials and basic-auth paths are unchanged.
  Backed by `OAuth2BearerToken` in `Peakflames.SharpBucket`, which upstream `SharpBucket` does
  not provide — it can only run the client-credentials grant itself, never accept a token that
  was obtained elsewhere. This is the seam per-caller credentials will ride on.
- `FakeBitbucketServer` records the `Authorization` header of every request it receives, so tests
  can assert what was actually put on the wire rather than what the client was configured with
- `Broker` config section: `Broker:Enabled` (default `false`) starts a SQLite-backed token store —
  `oauth_transactions`, `client_codes`, `upstream_tokens`, `jti_mappings`, `our_refresh_tokens`,
  `registered_clients`, `app_meta` — plus a background janitor that sweeps expired rows every
  minute. On its own this changes nothing observable; it is the storage layer the authorization
  server below is built on. WAL journaling and a busy timeout are applied to every connection; the
  database needs exactly one writer, so run at most one replica when enabled. Bitbucket
  access/refresh tokens are stored in plaintext (they must be replayed to Bitbucket verbatim);
  client codes, this server's own issued refresh tokens, and DCR client secrets are stored hashed,
  compared in constant time. If the configured `Broker:DatabasePath` directory is not writable, the
  server falls back to a temp path and logs a warning rather than crash-looping — data does not
  survive a restart in that fallback.
- With `Broker:Enabled`, this server becomes its own OAuth 2.1 authorization server, delegating the
  actual sign-in to Bitbucket: `GET /.well-known/oauth-authorization-server`, `GET /authorize`,
  `GET /oauth/callback`, `POST /token`, and `GET /.well-known/jwks.json`. Modeled on FastMCP's
  `OAuthProxy` — two independent PKCE pairs (the client's, verified at `/token`; this server's own,
  used only against Bitbucket), a consent-binding cookie defending `/oauth/callback` against a
  confused-deputy replay, and a `state`=transaction-id substitution so Bitbucket never sees the
  client's own `state`. Never forwards a client-supplied RFC 8707 `resource` parameter upstream —
  Bitbucket Cloud rejects it with `invalid_target` — binding it to this server's own issued JWT
  `aud` instead. `McpAuth`'s resource-server gate automatically trusts this server's own signing
  key (persisted in `app_meta`, survives a restart) once the broker is enabled, rather than trying
  to fetch its own discovery document over HTTP. `POST /register` (Dynamic Client Registration,
  RFC 7591) is implemented but ships disabled via `Broker:DcrEnabled` — `Broker:StaticClients`
  covers the pre-configured-`clientId` case Claude Code and similar clients already support.
  Real end-to-end verification against Bitbucket itself (not just the test-only fake upstream
  server this is covered by) is still pending the Bitbucket OAuth consumer request.

### Changed
- `ModelContextProtocol`/`ModelContextProtocol.AspNetCore` 0.7.0-preview.1 → 2.1.0 (all three
  projects)
- MCP HTTP transport is now stateless (`WithHttpTransport(o => o.Stateless = true)`) — no
  `Mcp-Session-Id` is issued
- Credential env vars (`BITBUCKET_MCP_USERNAME`, `BITBUCKET_MCP_API_TOKEN`,
  `BITBUCKET_MCP_CONSUMER_KEY`, `BITBUCKET_MCP_SECRET_KEY`) are now read through
  `IConfiguration` rather than `Environment.GetEnvironmentVariable` directly — same values at
  runtime, but testable without mutating process-wide environment state

### Removed
- **BREAKING:** the `/sse`, `/message`, and root `/` MCP mounts are gone. `/mcp` (Streamable
  HTTP) is the only endpoint — update any client still pointed at the old URLs. `python build.py
  mcp *` now connects to `/mcp`
- The fake-SSE GET-request workaround middleware for the Cline/TypeScript MCP SDK bug — no
  longer needed under the stateless transport
- Dead `Polarion`/`ReverseMarkdown`/`HtmlAgilityPack` package references and trimmer roots in
  `BitbucketRemoteMcpServer.csproj` (stale leftovers; this server has never depended on Polarion)

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
