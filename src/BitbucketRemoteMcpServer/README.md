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

## Optional: Token-Broker Storage (`Broker`)

Also **disabled by default**. This is the SQLite-backed storage layer that a later phase's
authorization server and per-user credential resolution are built on top of — on its own it does
not change how the server behaves; enabling it only opens/creates the database file and starts a
background sweep of expired rows.

```jsonc
"Broker": {
  "Enabled": false,
  "DatabasePath": "data/broker.db"   // relative paths resolve against the working directory
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
