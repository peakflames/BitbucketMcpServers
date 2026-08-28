# Bitbucket MCP Servers

This repository contains Model Context Protocol (MCP) server implementations for Bitbucket Cloud integration.

MCP Tools are available for Bitbucket operations, including:

- `list_pull_open_requests`: Gets all open pull requests in a Bitbucket repository, including draft status.
- `list_pull_requests`: Gets pull requests in a Bitbucket repository filtered by state (Open, Merged, Declined, Superseded), including draft status.
- `get_pull_request_comments`: Gets comments for a specific pull request.
- `get_pull_request_details`: Gets detailed information about a pull request including description, metadata (including draft status), and changed files.
- `read_file`: Reads the raw contents of a file in a repository at a given revision.
- `list_directory`: Shallow listing of files and directories in a repository path at a given revision.
- `search_code`: Searches code content across all repositories in the workspace.
- `list_repositories`: Lists repositories in the workspace, with optional name filtering.
- `list_branches`: Lists branches in a repository, with optional name filtering.
- `list_commits`: Lists commit history for a repository or branch.
- `get_commit`: Gets detailed information about a single commit by hash.

## Projects

- **BitbucketRemoteMcpServer**: ASP.NET Web API-based MCP server for server-based installations (Streamable HTTP transport)
- **BitbucketMcpServer**: Console-based MCP server for local workstation installations (stdio transport)

## Running via Docker & Linux Server (Recommended)

1. From your Linux server, create a directory for your configuration:

   ```bash
   mkdir -p /opt/bitbucket-mcp-server
   cd /opt/bitbucket-mcp-server
   ```

2. Pull the Docker image:

   ```bash
   docker pull peakflames/bitbucket-remote-mcp-server
   ```

3. Create an `appsettings.json` file tailored to your Bitbucket configuration:

   ```json
   {
     "BitbucketCloudConfig": {
       "AccountName": "your-workspace-name"
     }
   }
   ```

4. Run the container. See [Authentication](#authentication) below to choose and configure a credential method — the example below uses the default shared-credential mode:

   ```bash
   docker run -d \
     --name bitbucket-mcp-server \
     -p 8080:8080 \
     -e BITBUCKET_MCP_USERNAME="your_bitbucket_username" \
     -e BITBUCKET_MCP_API_TOKEN="your_bitbucket_app_password" \
     -v $(pwd)/appsettings.json:/app/appsettings.json \
     peakflames/bitbucket-remote-mcp-server
   ```

5. The server should now be running. MCP clients will connect using:
   - **Streamable HTTP Transport**: `http://{{your-server-ip}}:8080/mcp`

### Configuration Options (`appsettings.json`)

| Setting | Description | Required | Default |
|---------|-------------|----------|---------|
| `AccountName` | The Bitbucket workspace/account name | Yes | N/A |

Authentication credentials are retrieved from environment variables at startup — see
[Authentication](#authentication) below.

## Authentication

The server supports two ways to authenticate to Bitbucket, chosen by which environment variables
and config sections are set:

- **Shared credential (default)** — one identity, configured by the deployer, used for every
  caller.
- **Per-user OAuth (Broker)** — each caller signs in with their own Bitbucket account, and every
  tool call is scoped to that caller's own Bitbucket permissions.

### Shared credential (default)

The server supports two authentication methods: **OAuth 2.0 Client Credentials** and **Basic
Authentication**. Configure one of these methods using environment variables.

#### OAuth 2.0 Client Credentials (Recommended for Production)

OAuth 2.0 provides more secure authentication for server-to-server communication and is recommended for production deployments.

**Linux/macOS:**
```bash
export BITBUCKET_MCP_CONSUMER_KEY="your_consumer_key"
export BITBUCKET_MCP_SECRET_KEY="your_secret_key"
```

**Windows (PowerShell):**
```powershell
$env:BITBUCKET_MCP_CONSUMER_KEY = "your_consumer_key"
$env:BITBUCKET_MCP_SECRET_KEY = "your_secret_key"
```

**Windows (Command Prompt):**
```cmd
set BITBUCKET_MCP_CONSUMER_KEY=your_consumer_key
set BITBUCKET_MCP_SECRET_KEY=your_secret_key
```

**How to obtain OAuth 2.0 credentials:**
1. Log in to your Bitbucket workspace
2. Navigate to **Settings** > **OAuth consumers**
3. Click **Add consumer**
4. Configure the consumer with the necessary permissions (e.g., repositories read/write, pull requests)
5. Save and note your Consumer Key and Consumer Secret

#### Basic Authentication (Alternative Method)

Basic authentication uses your Bitbucket username and app password. This method is simpler to set up but less secure for production use.

**Linux/macOS:**
```bash
export BITBUCKET_MCP_USERNAME="your_username"
export BITBUCKET_MCP_API_TOKEN="your_app_password"
```

**Windows (PowerShell):**
```powershell
$env:BITBUCKET_MCP_USERNAME = "your_username"
$env:BITBUCKET_MCP_API_TOKEN = "your_app_password"
```

**Windows (Command Prompt):**
```cmd
set BITBUCKET_MCP_USERNAME=your_username
set BITBUCKET_MCP_API_TOKEN=your_app_password
```

**How to create an app password:**
1. Log in to Bitbucket
2. Click your profile avatar > **Personal settings**
3. Under **Access management**, click **App passwords**
4. Click **Create app password**
5. Give it a label and select the necessary permissions
6. Copy the generated app password (you won't be able to see it again)

#### Authentication Method Selection

The server automatically determines which authentication method to use based on the environment variables you set:

- **OAuth 2.0 is used** when `BITBUCKET_MCP_CONSUMER_KEY` and `BITBUCKET_MCP_SECRET_KEY` are set
- **Basic Authentication is used** when `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN` are set
- Recommended: configure **either** OAuth 2.0 credentials **or** Basic Auth credentials, not both — the server picks OAuth 2.0 if consumer key/secret are present, otherwise falls back to Basic Auth, so mixing the two is unambiguous but usually a sign of leftover config.

A shared credential (one of the two methods above) is required at startup, unless the Broker is
enabled — see below.

#### How It Works

1. **Configuration Loading**: On startup, the application loads the AccountName from `appsettings.json`
2. **Environment Variable Resolution**: At boot time in `Program.cs`, the application reads the authentication credentials from environment variables
3. **Authentication Method Selection**: The server automatically selects OAuth 2.0 if consumer key/secret are provided, otherwise falls back to Basic Authentication
4. **Client Creation**: For repo-scoped tools (e.g. `read_file`, `list_pull_requests`), a Bitbucket client is created using the shared credential and the repository slug passed as a tool argument. For workspace-scoped tools (`list_repositories`, `search_code`), the client is validated against the workspace itself instead — no repository name is needed

### Per-user OAuth (Broker)

Instead of one shared identity, the Broker turns the server into its own OAuth 2.1 authorization
server that delegates sign-in to Bitbucket. Each caller connects, authenticates as themselves
against Bitbucket, and every subsequent tool call resolves and uses that caller's own Bitbucket
access token — so two different authenticated callers hitting the same tool see results scoped to
their own real Bitbucket read permissions rather than one shared identity's.

The Broker is disabled by default and requires `McpAuth:Enabled` (the OAuth 2.1 resource-server
gate in front of `/mcp`) to also be enabled — without it, no caller would present a token for the
Broker to resolve, and the server refuses to start rather than accept tool calls it can't
authenticate.

```jsonc
"McpAuth": {
  "Enabled": true,
  "Issuer": "",              // ignored once Broker:Enabled is true — see below
  "ResourceUri": "",         // this server's own canonical URL, e.g. https://your-mcp-server-url/mcp
  "ScopesSupported": ["bitbucket:read"],
  "ClockSkewSeconds": 30
},
"Broker": {
  "Enabled": true,
  "DatabasePath": "data/broker.db",       // relative paths resolve against the working directory
  "IssuerUri": "",                        // this server's own base URL, e.g. https://your-mcp-server-url
  "UpstreamAuthorizeUrl": "https://bitbucket.org/site/oauth2/authorize",
  "UpstreamTokenUrl": "https://bitbucket.org/site/oauth2/access_token",
  "UpstreamUserInfoUrl": "https://api.bitbucket.org/2.0/user",
  "UpstreamClientId": "",                 // the Bitbucket OAuth consumer's key
  "UpstreamClientSecret": "",             // the Bitbucket OAuth consumer's secret — see below, do not put this inline
  "UpstreamScopes": ["account", "repository", "pullrequest"],
  "DcrEnabled": false,                    // POST /register (RFC 7591) — built, off by default
  "StaticClients": [],                    // pre-registered public clients: [{ "ClientId": "...", "RedirectUris": ["..."] }]
  "TransactionLifetimeMinutes": 15,
  "ClientCodeLifetimeMinutes": 5,
  "IssuedAccessTokenLifetimeMinutes": 60,
  "IssuedRefreshTokenLifetimeDays": 30
}
```

With `Broker:Enabled`, `McpAuth`'s resource-server gate automatically trusts this server's own
signing key (persisted in the database, so a restart doesn't invalidate outstanding tokens)
instead of fetching discovery/JWKS from `McpAuth:Issuer` — that setting still has to be a
syntactically valid URI to pass validation, but its value stops mattering once the Broker is on.

**Supplying `Broker:UpstreamClientSecret` (and any other Broker secret).** Do not put a real
secret value inline in `appsettings.json` — per this repo's convention, every secret is supplied
through an environment variable at boot, using the ASP.NET Core double-underscore (`__`)
convention to address a nested config key:

```bash
export Broker__UpstreamClientSecret="your_bitbucket_oauth_consumer_secret"
```

```powershell
$env:Broker__UpstreamClientSecret = "your_bitbucket_oauth_consumer_secret"
```

**Mounting the SQLite database.** `Broker:DatabasePath` defaults to `data/broker.db`, relative to
the working directory — `/app/data/broker.db` inside the container. Mount a persistent volume
there, or every user's stored token is lost on container restart (the server falls back to a
temp-directory path and logs a warning if the configured directory isn't writable — that fallback
does **not** survive a restart):

```bash
docker run -d \
  --name bitbucket-mcp-server \
  -p 8080:8080 \
  -e McpAuth__Enabled="true" \
  -e McpAuth__ResourceUri="https://your-mcp-server-url/mcp" \
  -e Broker__Enabled="true" \
  -e Broker__IssuerUri="https://your-mcp-server-url" \
  -e Broker__UpstreamClientId="your_bitbucket_oauth_consumer_key" \
  -e Broker__UpstreamClientSecret="your_bitbucket_oauth_consumer_secret" \
  -v $(pwd)/appsettings.json:/app/appsettings.json \
  -v $(pwd)/data:/app/data \
  peakflames/bitbucket-remote-mcp-server
```

**Requesting the Bitbucket OAuth consumer:** one **private** consumer per environment (Bitbucket's
Callback URL field is validated by prefix match, not exact match, so a `localhost` dev URL and a
real HTTPS host need separate consumers), Callback URL `<Broker:IssuerUri>/oauth/callback`,
permissions scoped to read-only Account/Repositories/Pull requests.

**`Broker:IssuerUri` and `McpAuth:ResourceUri` constraints** — these must be an absolute URI,
`https` outside the `Development` environment (plain `http` is only accepted in `Development`),
with no fragment, no path, and no trailing slash. The server enforces these with
`ValidateOnStart`, so a malformed value fails at boot with a clear error rather than at runtime.

**`Broker:StaticClients`** is the non-DCR way to pre-register a client (e.g. Claude Code with a
fixed `oauth.clientId`) — public clients only, no secret; PKCE is the confidentiality mechanism.
DCR-registered clients (`Broker:DcrEnabled: true`) live in the database instead.

The database needs exactly one writer, so run at most one replica when the Broker is enabled.
Bitbucket access/refresh tokens are stored in plaintext (they must be replayed to Bitbucket
verbatim); everything else that only needs to be verified — client codes, the refresh tokens this
server issues, DCR client secrets — is stored hashed. Encryption of the database file itself is a
volume-level concern, not something this application layer does.

## Configuring MCP Clients

### Cline Configuration

1. Open Cline's MCP settings UI
2. Click the "Remote Servers" tab
3. Add the following configuration:

   ```json
   {
     "mcpServers": {
       "Bitbucket": {
         "autoApprove": [],
         "disabled": false,
         "timeout": 60,
         "url": "http://{{your-server-ip}}:8080/mcp",
         "transportType": "streamableHttp"
       }
     }
   }
   ```

### Troubleshooting Remote Server

**"Environment variable not set" error:**
- For OAuth 2.0: Ensure both `BITBUCKET_MCP_CONSUMER_KEY` and `BITBUCKET_MCP_SECRET_KEY` are set
- For Basic Auth: Ensure both `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN` are set
- The application validates the required credentials at startup and will fail to start if they are not properly configured, unless `Broker:Enabled` is `true` — in that case no shared credential is needed
- Make sure you're using one complete authentication method, not mixing variables from both

**Connection errors:**
- Verify your Bitbucket credentials are correct
- For OAuth 2.0: Ensure the consumer key and secret are valid and the OAuth consumer is active in Bitbucket
- For Basic Auth: Ensure the app password has the necessary permissions for the repositories you're accessing
- Check that the account name in the configuration is correct
- For repo-scoped tools, verify that the repository name passed to the tool matches an actual repository in your account

**Permission errors:**
- For OAuth 2.0: Verify the OAuth consumer has the required scopes (e.g., repositories read/write, pull requests)
- For Basic Auth: Ensure your app password has permissions for all repositories you need to access
- Check that your Bitbucket user has access to the specified repository

## Contributing

For development setup, building instructions, Docker container publishing, and debugging information, see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

See [LICENSE](LICENSE) for details.
