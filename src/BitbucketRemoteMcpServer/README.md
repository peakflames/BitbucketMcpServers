# Bitbucket Remote MCP Server

This ASP.NET Web API application provides MCP (Model Context Protocol) server functionality for interacting with Bitbucket Cloud repositories.

## Configuration

The application uses `appsettings.json` for configuration with a single set of credentials that can be used to access any repository in your Bitbucket account.

### appsettings.json Structure

```json
{
  "BitbucketCloudConfig": {
    "AccountName": "your-workspace"
  }
}
```

### Configuration Properties

- **AccountName**: The Bitbucket workspace/account name (the only value stored in appsettings.json)
- **Username**: Retrieved from the `BITBUCKET_MCP_USERNAME` environment variable at startup
- **AppPassword**: Retrieved from the `BITBUCKET_MCP_API_TOKEN` environment variable at startup

### Environment Variables

You must set the following environment variables:

```bash
BITBUCKET_MCP_USERNAME=your_bitbucket_username
BITBUCKET_MCP_API_TOKEN=your_bitbucket_app_password
```

### Setting Environment Variables

#### Windows (PowerShell)
```powershell
$env:BITBUCKET_MCP_USERNAME = "your_username"
$env:BITBUCKET_MCP_API_TOKEN = "your_app_password"
```

#### Windows (Command Prompt)
```cmd
set BITBUCKET_MCP_USERNAME=your_username
set BITBUCKET_MCP_API_TOKEN=your_app_password
```

#### Linux/macOS
```bash
export BITBUCKET_MCP_USERNAME="your_username"
export BITBUCKET_MCP_API_TOKEN="your_app_password"
```

## Optional: OAuth 2.1 Resource Server (`McpAuth`) and Access Control (`Access`)

Both features are **disabled by default** — with no config, the server behaves exactly as it did
before either existed. This is Phase 1 of a multi-phase rollout; per-user credential passthrough
(a caller's own Bitbucket permissions, rather than the single shared credential above) is not yet
implemented. `Access:DisabledTools` exists to hide/deny a small set of tools in the meantime.

```jsonc
"McpAuth": {
  "Enabled": false,
  "Issuer": "",              // e.g. https://issuer.okta.example.invalid/oauth2/<asid>
  "MetadataAddress": null,   // optional override for the OIDC discovery document location
  "ResourceUri": "",         // this server's own canonical URL, e.g. https://your-mcp-server-url/mcp
  "ScopesSupported": ["bitbucket:read"],
  "ClockSkewSeconds": 30
},
"Access": {
  "Enabled": false,
  "AuditOnly": false,        // when true, DisabledTools is evaluated but never actually blocks
  "IdentityClaim": "email",  // reserved for a later phase; unused today
  "DisabledTools": []        // tool names to hide from tools/list and deny on tools/call
}
```

- `McpAuth:Issuer` must use `https` unless `ASPNETCORE_ENVIRONMENT=Development`.
- `Access:Enabled` requires `McpAuth:Enabled` — without authentication there is no way to scope
  `Access` to authenticated callers only, so the server refuses to start rather than apply it
  unconditionally to everyone.
- When `McpAuth:Enabled` is `true`, every `POST /mcp` call requires a valid bearer token issued by
  `Issuer` with the `bitbucket:read` scope; the server never issues tokens itself.

## Optional: Token Broker (`Broker`)

Also **disabled by default**. Enabling it turns this server into its own OAuth 2.1 authorization
server that delegates sign-in to Bitbucket — the per-user credential passthrough `Access` above
exists as a stopgap for. On its own, enabling it opens/creates the SQLite database, starts a
background sweep of expired rows, and maps `/authorize`, `/oauth/callback`, `/token`, and the
`/.well-known/*` metadata/JWKS endpoints; per-user credential resolution for the MCP tools
themselves is a later phase, still to come.

```jsonc
"Broker": {
  "Enabled": false,
  "DatabasePath": "data/broker.db",       // relative paths resolve against the working directory
  "IssuerUri": "",                        // this server's own base URL, e.g. https://your-mcp-server-url
  "UpstreamAuthorizeUrl": "https://bitbucket.org/site/oauth2/authorize",
  "UpstreamTokenUrl": "https://bitbucket.org/site/oauth2/access_token",
  "UpstreamUserInfoUrl": "https://api.bitbucket.org/2.0/user",
  "UpstreamClientId": "",                 // the Bitbucket OAuth consumer's key
  "UpstreamClientSecret": "",             // the Bitbucket OAuth consumer's secret
  "UpstreamScopes": ["account", "repository", "pullrequest"],
  "DcrEnabled": false,                    // POST /register (RFC 7591) — built, off by default
  "StaticClients": [],                    // pre-registered public clients: [{ "ClientId": "...", "RedirectUris": ["..."] }]
  "TransactionLifetimeMinutes": 15,
  "ClientCodeLifetimeMinutes": 5,
  "IssuedAccessTokenLifetimeMinutes": 60,
  "IssuedRefreshTokenLifetimeDays": 30
}
```

- The database needs exactly one writer, so run at most one replica when this is enabled.
- If the configured directory is not writable, the server falls back to a path under the OS temp
  directory and logs a warning — data does **not** survive a restart in that fallback. Fix the
  volume mount rather than relying on it.
- Bitbucket access/refresh tokens are stored in plaintext (they must be replayed to Bitbucket
  verbatim); everything else that only needs to be verified — client codes, the refresh tokens
  this server issues, DCR client secrets — is stored hashed. Encryption of the database file
  itself is a volume-level concern, not something this application layer does.
- With `Broker:Enabled`, `McpAuth`'s resource-server gate automatically trusts this server's own
  signing key (persisted in the database, so a restart doesn't invalidate outstanding tokens)
  instead of fetching discovery/JWKS from `McpAuth:Issuer` — that setting still has to be a
  syntactically valid URI to pass validation, but its value stops mattering once the broker is on.
- `Broker:StaticClients` is the non-DCR way to pre-register a client (e.g. Claude Code with a
  fixed `oauth.clientId`) — public clients only, no secret; PKCE is the confidentiality mechanism.
  DCR-registered clients live in the database instead and are only reachable when
  `Broker:DcrEnabled` is `true`.
- Requesting the Bitbucket OAuth consumer: one **private** consumer per environment (Bitbucket's
  Callback URL field is validated by prefix match, not exact match, so a `localhost` dev URL and a
  real HTTPS host need separate consumers), Callback URL `<IssuerUri>/oauth/callback`, permissions
  scoped to read-only Account/Repositories/Pull requests.

## Using the MCP Server

The application exposes MCP tools that accept a repository name as a parameter. The repository slug is provided as an argument when calling the tool, not in the URL path.

### Endpoint Patterns

For SSE:
```
https://your-mcp-server-url/sse
```
For Streamable HTTP:
```
https://your-mcp-server-url/
```

### Available Tools

| Tool Name | Description |
|---|---|
| list_pull_open_requests | Gets all open pull requests in a Bitbucket repository. |


## How It Works

1. **Configuration Loading**: On startup, the application loads the AccountName from `appsettings.json`
2. **Environment Variable Resolution**: At boot time in `Program.cs`, the application reads `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN` environment variables and validates they are set
3. **Tool Invocation**: When an MCP tool is called, the repository name is passed as a function argument
4. **Client Creation**: A Bitbucket client is created using:
   - The credentials resolved from environment variables at startup
   - The account name from the configuration
   - The repository slug from the tool's function argument



## Troubleshooting

### "Environment variable not set" error
- Ensure both `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN` environment variables are set before starting the application
- The application validates these at startup and will fail to start if they are not set

### Connection errors
- Verify your Bitbucket credentials are correct
- Ensure the API Key has the necessary permissions for the repositories you're accessing
- Check that the account name in the configuration is correct
- Verify that the repository name passed to the tool matches an actual repository in your account

### Permission errors
- Ensure your API Key has permissions for all repositories you need to access
- Check that your Bitbucket user has access to the specified repository
- Verify the API Key has the necessary scopes (e.g., pull requests read/write)

## Running the Server

### Using Visual Studio Code
1. Set the required environment variables in `.vscode/launch.json` or your system environment
2. Press F5 or select "BitbucketRemoteMcpServer" from the debug configuration dropdown
3. The server will start and listen on the configured port

### Using Command Line
```bash
# Set environment variables first
export BITBUCKET_MCP_USERNAME="your_username"
export BITBUCKET_MCP_API_TOKEN="your_api_key"

# Run the server
dotnet run --project src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj
```

### Using Docker (if configured)
```bash
docker run -e BITBUCKET_MCP_USERNAME="your_username" \
           -e BITBUCKET_MCP_API_TOKEN="your_api_key" \
           -p 5000:5000 \
           bitbucket-mcp-server
