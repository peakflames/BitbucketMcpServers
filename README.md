# Bitbucket MCP Server

This project contains an MCP (Model Context Protocol) server that can interact with Bitbucket.
It includes an example console application (`BitbucketMcpServer`) that demonstrates how to use the `SharpBucket` library to connect to Bitbucket and retrieve pull request information.

## BitbucketMcpServer Configuration

The `BitbucketMcpServer` console application requires configuration to connect to your Bitbucket account and target repository. This configuration can be provided via command-line arguments or environment variables. Command-line arguments take precedence over environment variables.

### Required Parameters:

1. **Bitbucket Username**
    * Command-Line: `-u <your_username>` or `--username <your_username>`
    * Environment Variable: `BITBUCKET_USERNAME`
    * Description: Your Bitbucket account username (used for Basic Authentication).

2. **Bitbucket App Password**
    * Command-Line: `-p <your_app_password>` or `--password <your_app_password>`
    * Environment Variable: `BITBUCKET_APP_PASSWORD`
    * Description: An App Password generated from your Bitbucket account settings. This is used instead of your primary account password for security.

3. **Bitbucket Account Name (Workspace ID)**
    * Command-Line: `-a <account_name>` or `--account <account_name>`
    * Environment Variable: `BITBUCKET_ACCOUNT_NAME`
    * Description: The workspace ID or account name that owns the repository (e.g., `myworkspace` in `https://bitbucket.org/myworkspace/myrepo`).

4. **Bitbucket Repository Slug**
    * Command-Line: `-r <repo_slug>` or `--repo <repo_slug>`
    * Environment Variable: `BITBUCKET_REPO_SLUG`
    * Description: The URL-friendly name of the repository (e.g., `myrepo` in `https://bitbucket.org/myworkspace/myrepo`).

### Example Usage:

**Using Command-Line Arguments:**

```shell
dotnet run --project src/BitbucketMcpServer/BitbucketMcpServer.csproj -- -u "yourusername" -p "yourAppPassword" -a "yourAccountName" -r "yourRepoSlug"
```

**Using Environment Variables:**

1. Set the environment variables:

    ```sh
    # Example for PowerShell
    $env:BITBUCKET_USERNAME="yourusername"
    $env:BITBUCKET_APP_PASSWORD="yourAppPassword"
    $env:BITBUCKET_ACCOUNT_NAME="yourAccountName"
    $env:BITBUCKET_REPO_SLUG="yourRepoSlug"

    # Example for bash/zsh
    export BITBUCKET_USERNAME="yourusername"
    export BITBUCKET_APP_PASSWORD="yourAppPassword"
    export BITBUCKET_ACCOUNT_NAME="yourAccountName"
    export BITBUCKET_REPO_SLUG="yourRepoSlug"
    ```

1. Run the application:

    ```sh
    dotnet run --project src/BitbucketMcpServer/BitbucketMcpServer.csproj
    ```

If any of these parameters are missing, the application will log an error and skip the Bitbucket API interaction.

## Building and Running

To build the solution:

```sh
dotnet build
```

To run the example server (ensure configuration is provided as above):

```sh
dotnet run --project src/BitbucketMcpServer/BitbucketMcpServer.csproj
```

## Build the standalone executable for local MCP

```sh
dotnet publish .\src\BitbucketMcpServer\BitbucketMcpServer.csproj -o publish
```

## Dependencies

* [SharpBucket](https://github.com/MitjaBezensek/SharpBucket): A .NET wrapper for the Bitbucket Cloud's REST APIs.
* [Serilog](https://serilog.net/): For logging.
* [ModelContextProtocol](https://www.npmjs.com/package/model-context-protocol): For MCP server integration (though this example primarily focuses on `SharpBucket` usage).
