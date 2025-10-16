# Bitbucket MCP Server

This project contains an MCP (Model Context Protocol) server that can interact with Bitbucket.

## Getting Started

### BitbucketMcpServer Configuration

The `BitbucketMcpServer` console application requires configuration to connect to your Bitbucket account and target repository. This configuration can be provided via command-line arguments or environment variables. Command-line arguments take precedence over environment variables.

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

## Contributing

For development setup, building instructions, Docker container publishing, and debugging information, see [CONTRIBUTING.md](CONTRIBUTING.md).
