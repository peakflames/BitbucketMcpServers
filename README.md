# Bitbucket MCP Servers

This repository contains Model Context Protocol (MCP) server implementations for Bitbucket Cloud integration.

MCP Tools are available for Bitbucket operations, including:

- `list_pull_open_requests`: Gets all open pull requests in a Bitbucket repository.
- `get_pull_request_comments`: Gets comments for a specific pull request.

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
       "AccountName": "your-workspace-name",
       "Username": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_USERNAME",
       "AppPassword": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_API_TOKEN"
     }
   }
   ```

4. Run the Docker container:

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
| `Username` | Placeholder indicating which environment variable contains the username | Yes | N/A |
| `AppPassword` | Placeholder indicating which environment variable contains the app password | Yes | N/A |

### Environment Variables

You must set the following environment variables:

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

### How It Works

1. **Configuration Loading**: On startup, the application loads configuration from `appsettings.json`
2. **Environment Variable Resolution**: Sensitive credentials are resolved from environment variables
3. **Tool Invocation**: When an MCP tool is called, the repository name is passed as a function argument
4. **Client Creation**: A Bitbucket client is created using the configured credentials and the repository slug from the tool's argument

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
- Ensure both `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN` environment variables are set before starting the application
- Check that the environment variable names in `appsettings.json` match the actual environment variable names

**Connection errors:**
- Verify your Bitbucket credentials are correct
- Ensure the app password has the necessary permissions for the repositories you're accessing
- Check that the account name in the configuration is correct
- Verify that the repository name passed to the tool matches an actual repository in your account

**Permission errors:**
- Ensure your app password has permissions for all repositories you need to access
- Check that your Bitbucket user has access to the specified repository
- Verify the app password has the necessary scopes (e.g., pull requests read/write)

## Contributing

For development setup, building instructions, Docker container publishing, and debugging information, see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

See [LICENSE](LICENSE) for details.
