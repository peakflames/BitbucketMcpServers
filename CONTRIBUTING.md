# Contributing to Bitbucket MCP Server

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- Docker (for container deployment)
- Git

### Building and Testing

Use the Python build automation script for build, run, and test operations:

```bash
python build.py build    # Build the solution
python build.py start    # Build and start in the background (port 5107)
python build.py test     # Run the test suite
```

See [CLAUDE.md](CLAUDE.md) for the full command reference (logs, MCP client commands, etc.).

Alternatively, build directly with `dotnet`:

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
3. **Publish**: Pushes to Docker Hub at `peakflames/bitbucket-remote-mcp-server:<VERSION>`, and also
   retags and pushes that same image as `:latest`
4. **Sync README**: The root `README.md` is synced to the Docker Hub repository description

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

Both the version-specific tag (e.g., `0.0.2`, `1.0.0`) and `latest` are published on every
release. To pin a specific version:

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

1. Start the MCP Server project (`python build.py start`, port 5107 by default)
2. From a terminal, run `npx @modelcontextprotocol/inspector`
3. From your browser, navigate to the URL the inspector prints (typically `http://localhost:6274`)
4. Configure the inspector to connect to the server:
   - TransportType: streamable http
   - URL: http://localhost:5107/mcp

## Code Standards

When contributing to this project:
- Follow C# naming conventions and best practices
- Ensure all code builds successfully (`python build.py build`) and the test suite passes (`python build.py test`)
- Test Docker builds locally before pushing tags
- Update documentation as needed

## Submitting Changes

1. Create a feature branch from `develop`
2. Make your changes and test thoroughly
3. Commit with clear, descriptive messages
4. Push to your branch and create a pull request
5. Ensure all CI/CD checks pass
