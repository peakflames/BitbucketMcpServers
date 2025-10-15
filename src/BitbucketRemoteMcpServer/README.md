# Bitbucket Remote MCP Server

This ASP.NET Web API application provides MCP (Model Context Protocol) server functionality for interacting with Bitbucket Cloud repositories.

## Configuration

The application uses `appsettings.json` for configuration with a single set of credentials that can be used to access any repository in your Bitbucket account.

### appsettings.json Structure

```json
{
  "BitbucketCloudConfig": {
    "AccountName": "flyarcher",
    "Username": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_USERNAME",
    "AppPassword": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_API_TOKEN"
  }
}
```

### Configuration Properties

- **AccountName**: The Bitbucket workspace/account name
- **Username**: Placeholder indicating which environment variable contains the username
- **AppPassword**: Placeholder indicating which environment variable contains the API Key

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

1. **Configuration Loading**: On startup, the application loads the single configuration from `appsettings.json`
2. **Environment Variable Resolution**: When a client factory is created, it resolves environment variables for sensitive data
3. **Tool Invocation**: When an MCP tool is called, the repository name is passed as a function argument
4. **Client Creation**: A Bitbucket client is created using:
   - The credentials from the configuration
   - The account name from the configuration
   - The repository slug from the tool's function argument



## Troubleshooting

### "Environment variable not set" error
- Ensure both `BITBUCKET_MCP_USERNAME` and `BITBUCKET_MCP_API_TOKEN` environment variables are set before starting the application
- Check that the environment variable names in `appsettings.json` match the actual environment variable names

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
