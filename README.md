# Bitbucket MCP Servers

This repository contains Model Context Protocol (MCP) server implementations for Bitbucket Cloud integration.

MCP Tools are available for Bitbucket operations, including:

- `list_pull_open_requests`: Gets all open pull requests in a Bitbucket repository, including draft status.
- `list_pull_requests`: Gets pull requests in a Bitbucket repository filtered by state (Open, Merged, Declined, Superseded), including draft status.
- `get_pull_request_comments`: Gets comments for a specific pull request.
- `get_pull_request_details`: Gets detailed information about a pull request including description, metadata (including draft status), and changed files.

## Projects

- **BitbucketRemoteMcpServer**: ASP.NET Web API-based MCP server for server-based installations (Streamable HTTP or SSE transport)
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

4. Run the Docker container with your chosen authentication method:

   **Using OAuth 2.0 (Recommended):**
   ```bash
   docker run -d \
     --name bitbucket-mcp-server \
     -p 8080:8080 \
     -e BITBUCKET_MCP_CONSUMER_KEY="your_consumer_key" \
     -e BITBUCKET_MCP_SECRET_KEY="your_secret_key" \
     -v $(pwd)/appsettings.json:/app/appsettings.json \
     peakflames/bitbucket-remote-mcp-server
   ```

   **Using Basic Authentication:**
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
   - **Streamable HTTP Transport**: `http://{{your-server-ip}}:8080/`
   - **SSE Transport**: `http://{{your-server-ip}}:8080/sse`

### Configuration Options (`appsettings.json`)

The server uses `appsettings.json` for configuration with a single set of credentials that can access any repository in your Bitbucket account.

| Setting | Description | Required | Default |
|---------|-------------|----------|---------|
| `AccountName` | The Bitbucket workspace/account name | Yes | N/A |

**Note:** Authentication credentials (either OAuth 2.0 or Basic Auth) are retrieved from environment variables at startup (see Environment Variables section below).

### Environment Variables

The server supports two authentication methods: **OAuth 2.0 Client Credentials** and **Basic Authentication**. You must configure one of these methods using environment variables.

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
- You must provide **either** OAuth 2.0 credentials **or** Basic Auth credentials (not both)

### How It Works

1. **Configuration Loading**: On startup, the application loads the AccountName from `appsettings.json`
2. **Environment Variable Resolution**: At boot time in `Program.cs`, the application reads the authentication credentials from environment variables:
   - For OAuth 2.0: `BITBUCKET_MCP_CONSUMER_KEY` and `BITBUCKET_MCP_SECRET_KEY`
   - For Basic Auth: `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN`
3. **Authentication Method Selection**: The server automatically selects OAuth 2.0 if consumer key/secret are provided, otherwise falls back to Basic Authentication
4. **Tool Invocation**: When an MCP tool is called, the repository name is passed as a function argument
5. **Client Creation**: A Bitbucket client is created using the credentials resolved from environment variables at startup and the repository slug from the tool's argument

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
         "url": "http://{{your-server-ip}}:8080/sse",
         "transportType": "sse"
       }
     }
   }
   ```

### Troubleshooting Remote Server

**"Environment variable not set" error:**
- For OAuth 2.0: Ensure both `BITBUCKET_MCP_CONSUMER_KEY` and `BITBUCKET_MCP_SECRET_KEY` are set
- For Basic Auth: Ensure both `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN` are set
- The application validates the required credentials at startup and will fail to start if they are not properly configured
- Make sure you're using one complete authentication method, not mixing variables from both

**Connection errors:**
- Verify your Bitbucket credentials are correct
- For OAuth 2.0: Ensure the consumer key and secret are valid and the OAuth consumer is active in Bitbucket
- For Basic Auth: Ensure the app password has the necessary permissions for the repositories you're accessing
- Check that the account name in the configuration is correct
- Verify that the repository name passed to the tool matches an actual repository in your account

**Permission errors:**
- For OAuth 2.0: Verify the OAuth consumer has the required scopes (e.g., repositories read/write, pull requests)
- For Basic Auth: Ensure your app password has permissions for all repositories you need to access
- Check that your Bitbucket user has access to the specified repository

## Contributing

For development setup, building instructions, Docker container publishing, and debugging information, see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

See [LICENSE](LICENSE) for details.
