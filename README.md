# Bitbucket MCP Server

This project contains an MCP (Model Context Protocol) server that can interact with Bitbucket.

## BitbucketMcpServer Configuration

The `BitbucketMcpServer` console application requires configuration to connect to your Bitbucket account and target repository. This configuration can be provided via command-line arguments or environment variables. Command-line arguments take precedence over environment variables.

## Building and Running

To build the solution:

```sh
dotnet build
```

## Build the standalone executable for local MCP

```sh
dotnet publish .\src\BitbucketMcpServer\BitbucketMcpServer.csproj -o publish
```

## Example Usage

### Cline Setup

1. Build the standalone executable for local MCP
1. Copy the standalone executable to a directory in your PATH
1. Open the Cline MCP Configuation file (`cline_mcp_settings.json`) in Visual Studio Code.
1. Add the following configuration:

    ```json
    {
        "Bitbucket": {
            "autoApprove": [],
            "disabled": false,
            "timeout": 60,
            "command": "BitbucketMcpServer",
            "args": [
                "-u",
                "{{ bitbucket_username }}",
                "-p",
                "{{ bitbucket_app_password }}",
                "-a",
                "{{ bitbucket_account_name }}",
                "-r",
                "{{ bitbucket_repo_name }}"
            ],
            "transportType": "stdio"
            }
    }
    ```

## Building the Projects

### Prerequisites

- .NET 9.0 SDK or later
- Docker (for container deployment)

### Building Locally

To build the projects locally:

```bash
dotnet build BitbucketMcpServers.sln
```

### Building Docker Image

1. Roll the version and image tag by setting the `Version` & `ContainerImageTag` properties in `src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj`
1. Build the project and image locally:

```bash
dotnet publish src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj /t:PublishContainer -r linux-x64 
```

### Publishing to a Docker Registry

1. Roll the version and image tag by setting the `Version` & `ContainerImageTag` properties in `src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj`
1. Build the project and image and publish to your Docker registry:

```bash
dotnet publish src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj /t:PublishContainer -r linux-x64 
docker push peakflames/bitbucket-remote-mcp-server:{{VERSION}}
```

## Debugging the Streamable HTTP MCP Server

1. Start the MCP Server project
1. From a terminal, run `npx @modelcontextprotocol/inspector`
1. From you browser, navigate to `http://localhost:{{PORT}}`
1. Configure the inspector to connect to the server
   i. TransportType: streamable http
   i. URL: http://localhost:5107/