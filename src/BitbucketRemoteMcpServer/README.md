# Bitbucket Remote MCP Server

This ASP.NET Web API application provides MCP (Model Context Protocol) server functionality for interacting with Bitbucket Cloud repositories.

## Configuration

The application uses `appsettings.json` for configuration with support for multiple Bitbucket repositories. Sensitive credentials (username and app password) are obtained from environment variables for security.

### appsettings.json Structure

```json
{
  "BitbucketCloudConfig": [
    {
      "RepoSlug": "m001_fcc_sw",
      "AccountName": "flyarcher",
      "Username": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_0_USERNAME",
      "AppPassword": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_0_API_TOKEN",
      "Default": true
    },
    {
      "RepoSlug": "m001_battery_mgmt_unit",
      "AccountName": "flyarcher",
      "Username": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_1_USERNAME",
      "AppPassword": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_1_API_TOKEN",
      "Default": false
    }
  ]
}
```

### Configuration Properties

- **RepoSlug**: The repository slug (used as the route parameter in the URL)
- **AccountName**: The Bitbucket workspace/account name
- **Username**: Placeholder indicating which environment variable contains the username
- **AppPassword**: Placeholder indicating which environment variable contains the app password
- **Default**: Boolean flag indicating if this is the default configuration (only one can be true)

### Environment Variables

For each repository configuration, you must set the corresponding environment variables:

#### For the first repository (m001_fcc_sw):
```bash
BITBUCKET_MCP_0_USERNAME=your_bitbucket_username
BITBUCKET_MCP_0_API_TOKEN=your_bitbucket_app_password
```

#### For the second repository (m001_battery_mgmt_unit):
```bash
BITBUCKET_MCP_1_USERNAME=your_bitbucket_username
BITBUCKET_MCP_1_API_TOKEN=your_bitbucket_app_password
```

### Setting Environment Variables

#### Windows (PowerShell)
```powershell
$env:BITBUCKET_MCP_0_USERNAME = "your_username"
$env:BITBUCKET_MCP_0_API_TOKEN = "your_app_password"
$env:BITBUCKET_MCP_1_USERNAME = "your_username"
$env:BITBUCKET_MCP_1_API_TOKEN = "your_app_password"
```

#### Windows (Command Prompt)
```cmd
set BITBUCKET_MCP_0_USERNAME=your_username
set BITBUCKET_MCP_0_API_TOKEN=your_app_password
set BITBUCKET_MCP_1_USERNAME=your_username
set BITBUCKET_MCP_1_API_TOKEN=your_app_password
```

#### Linux/macOS
```bash
export BITBUCKET_MCP_0_USERNAME="your_username"
export BITBUCKET_MCP_0_API_TOKEN="your_app_password"
export BITBUCKET_MCP_1_USERNAME="your_username"
export BITBUCKET_MCP_1_API_TOKEN="your_app_password"
```

## Route-Based Repository Selection

The application uses route-based configuration selection via the `repo_slug` parameter:

### Endpoint Pattern
```
https://your-mcp-server-url/{repo_slug}/sse
```

### Examples
- `https://your-mcp-server-url/m001_fcc_sw/sse` → Uses the first repository configuration
- `https://your-mcp-server-url/m001_battery_mgmt_unit/sse` → Uses the second repository configuration
- `https://your-mcp-server-url/sse` → Uses the default repository configuration (marked with `Default: true`)

## How It Works

1. **Configuration Loading**: On startup, the application loads all repository configurations from `appsettings.json`
2. **Environment Variable Resolution**: When a client factory is created, it resolves environment variables for sensitive data
3. **Route Matching**: The factory uses `IHttpContextAccessor` to get the `repo_slug` from the route
4. **Configuration Selection**: 
   - If a route parameter is provided, it matches against the `RepoSlug` property (case-insensitive)
   - If no match is found or no route parameter is provided, it uses the default configuration
   - If no default is configured, an error is thrown
5. **Client Creation**: A Bitbucket client is created with the selected configuration

## Adding New Repositories

To add a new repository:

1. Add a new configuration object to the `BitbucketCloudConfig` array in `appsettings.json`:
```json
{
  "RepoSlug": "new_repo_name",
  "AccountName": "your_account",
  "Username": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_2_USERNAME",
  "AppPassword": "OBTAIN_FROM_ENV_VAR_BITBUCKET_MCP_2_API_TOKEN",
  "Default": false
}
```

2. Set the corresponding environment variables:
```bash
BITBUCKET_MCP_2_USERNAME=your_username
BITBUCKET_MCP_2_API_TOKEN=your_app_password
```

3. Access the repository via: `https://your-mcp-server-url/new_repo_name/sse`

## Security Best Practices

✅ **Never commit credentials to source control**
✅ **Always use environment variables for sensitive data**
✅ **Use Bitbucket App Passwords instead of account passwords**
✅ **Limit App Password permissions to only what's needed**
✅ **Rotate credentials regularly**

## Creating a Bitbucket App Password

1. Log in to Bitbucket
2. Click on your profile picture → **Personal settings**
3. Under **Access management**, click **App passwords**
4. Click **Create app password**
5. Give it a label and select the required permissions
6. Copy the generated password (you won't be able to see it again)
7. Use this password in your environment variables

## Troubleshooting

### "Environment variable not set" error
- Ensure all required environment variables are set before starting the application
- Check that the environment variable names in `appsettings.json` match the actual environment variable names

### "No configuration found" error
- Verify that at least one configuration has `Default: true`
- Check that the `repo_slug` in the URL matches a `RepoSlug` in the configuration

### Connection errors
- Verify your Bitbucket credentials are correct
- Ensure the App Password has the necessary permissions
- Check that the account name and repository slug are correct
