# Contributing to Bitbucket MCP Server

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- Docker (for container deployment)
- Git

### Building the Solution

To build the entire solution:

```bash
dotnet build BitbucketMcpServers.sln
```

### Building the Standalone Executable

To build the standalone executable for local MCP:

```sh
dotnet publish .\src\BitbucketMcpServer\BitbucketMcpServer.csproj -o publish
```

## Building and Publishing Docker Containers

### Building Locally

For local development and testing, you can build the Docker image without publishing:

```bash
dotnet publish src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj \
  /t:PublishContainer \
  --os linux --arch x64
```

This builds the image locally using the version specified in the `.csproj` file. The image will be available in your local Docker daemon.

### Automated Docker Publishing (GitHub Actions)

The repository includes an automated GitHub Actions workflow that builds and publishes Docker containers when version tags are pushed to the repository.

#### How It Works

1. **Trigger**: Push a git tag matching the pattern `v*` (e.g., `v0.0.2`, `v1.0.0`)
2. **Build**: Workflow automatically builds the Docker container for linux-x64 architecture
3. **Publish**: Pushes to Docker Hub at `peakflames/bitbucket-remote-mcp-server:<VERSION>`

#### Publishing a New Version

To publish a new version:

```bash
# Create and push a version tag
git tag v0.0.3
git push origin v0.0.3
```

The GitHub Actions workflow will automatically:
- Extract the version from the tag (removes 'v' prefix)
- Build the .NET project with that version
- Create and push the Docker image to Docker Hub with the version tag

#### Requirements

The workflow requires the following GitHub organization secrets to be configured:
- `DOCKERHUB_USERNAME`: Your Docker Hub username
- `DOCKERHUB_TOKEN`: A Docker Hub access token

These are already configured at the organization level for public repositories.

#### Image Tags

Only version-specific tags are published (e.g., `0.0.2`, `1.0.0`). To use a specific version:

```bash
docker pull peakflames/bitbucket-remote-mcp-server:0.0.2
```

### Publishing to Docker Registry Manually (Advanced)

**⚠️ Note**: Manual publishing is not recommended for production releases. Use the automated GitHub Actions workflow instead.

If you need to manually publish for testing or special circumstances:

1. Ensure you're authenticated to Docker Hub:
   ```bash
   docker login
   ```

2. Build and publish the container:
   ```bash
   dotnet publish src/BitbucketRemoteMcpServer/BitbucketRemoteMcpServer.csproj \
     /t:PublishContainer \
     --os linux --arch x64 \
     /p:ContainerRegistry=docker.io \
     /p:Version=<YOUR_VERSION> \
     /p:ContainerImageTag=<YOUR_VERSION>
   ```

Replace `<YOUR_VERSION>` with your desired version number (e.g., `0.0.3-dev`).

## Debugging

### Debugging the Streamable HTTP MCP Server

1. Start the MCP Server project
2. From a terminal, run `npx @modelcontextprotocol/inspector`
3. From your browser, navigate to `http://localhost:{{PORT}}`
4. Configure the inspector to connect to the server:
   - TransportType: streamable http
   - URL: http://localhost:5107/

## Code Standards

When contributing to this project:
- Follow C# naming conventions and best practices
- Ensure all code builds successfully with `dotnet build`
- Test Docker builds locally before pushing tags
- Update documentation as needed

## Submitting Changes

1. Create a feature branch from `main`
2. Make your changes and test thoroughly
3. Commit with clear, descriptive messages
4. Push to your branch and create a pull request
5. Ensure all CI/CD checks pass
