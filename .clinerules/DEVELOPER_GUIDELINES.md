# BitbucketMcpServers Developer Guidelines

This document outlines the essential rules and conventions for the BitbucketMcpServers project. Follow these guidelines to maintain consistency and ensure proper functionality.

## Project Structure

- **BitbucketMcpTools**: Core library with tools for interacting with Bitbucket
- **BitbucketMcpServer**: Console application with stdio transport
- **BitbucketRemoteMcpServer**: Web application with HTTP transport

## Build and Test

Use the Python build automation script for all build and run operations:

```bash
python build.py build    # Build solution (auto-stops running app)
python build.py start    # Build and start in background (port 5107)
python build.py stop     # Stop background application
python build.py status   # Check if application is running
python build.py run      # Run in foreground (blocks terminal)
```

### MCP Commands (Model Context Protocol)
```bash
python build.py mcp ping                              # Check MCP server connectivity
python build.py mcp info                              # Show MCP server information
python build.py mcp tools                             # List available MCP tools
python build.py mcp call <tool> '{"arg": "value"}'    # Call an MCP tool with JSON args
```
**Examples:**
```bash
python build.py mcp call list_pull_open_requests '{"repoName": "my-repo"}'
python build.py mcp call get_pull_request_details '{"repoName": "my-repo", "pullRequestId": "123"}'
```
**Note:** Use double quotes for JSON argument keys/values on Windows. If tools error with authentication failures, check that credentials are configured in `src/BitbucketRemoteMcpServer/appsettings.json`.

### Log Commands (for debugging)
```bash
python build.py log                      # Show last 50 lines
python build.py log <pattern>            # Search for regex pattern
python build.py log --tail <n>           # Show last n lines
python build.py log --level error        # Filter by level (error/warn/info/debug)
python build.py log --tail 100 --level error  # Combine options
```

### URLs (when running)
- http://localhost:5107 - Landing page
- http://localhost:5107/sse - MCP SSE endpoint (for AI tool integration)

### Key Behaviors
- **`build`** auto-stops any running instance (prevents Windows file lock errors)
- **`start`** auto-builds before launching (always runs latest code)
- **`start`** runs in background, freeing terminal for mcp/verification commands
- **`stop`** gracefully terminates, then force-kills if needed

### Prerequisites
```bash
pip install psutil fastmcp
```

## CRITICAL: Verification Before Commit Rule

**NEVER commit code changes before the user has verified them!**

A successful build (compile) does NOT equal working code. The workflow MUST be:

1. **Implement** - Make the code changes
2. **Build** - Run `python build.py build` to verify compilation
3. **Start App** - Run `python build.py start` to launch in background
4. **Verify** - Use MCP tools or manual testing to confirm functionality
5. **Commit** - ONLY after verification passed

**Why this matters:**
- Compiled code ≠ correct behavior
- API changes need endpoint verification
- Business logic needs functional testing
- Committing untested code pollutes git history with potential bugs

**Verification Workflow Example:**
```bash
python build.py start                                          # Build & start in background
python build.py mcp ping                                       # Verify MCP connectivity
python build.py mcp tools                                      # Verify tools are registered
python build.py mcp call list_pull_open_requests '{"repoName": "my-repo"}'  # Test a tool
python build.py log --level error                              # Check for errors
# ... additional verification ...
git add <specific files> && git commit -m "feat: ..."          # Commit after verification
python build.py stop                                           # Stop when done (optional)
```

## Git Workflow

- Prefer `--no-ff` when merging for any branches to preserve commit history
- Prefer to ask user for approval before pushing to origin unless explicitly requested
- Always explicitly exclude `appsettings.json` from staging (see Security Rule below)
- Use explicit file paths in `git add` commands rather than wildcards

## Adding New Configuration

1. **Update appsettings.json**:
   - Add new configuration properties to the appropriate section in `src/BitbucketRemoteMcpServer/appsettings.json`
   - For new Bitbucket configurations, add them to the appropriate config section
   - Include any necessary filters for the configuration

2. **Update BitbucketProjectConfig**:
   - If adding new configuration properties, update the `BitbucketProjectConfig` class in `src/BitbucketMcpTools/BitbucketProjectConfig.cs`
   - Ensure properties have proper XML documentation

3. **Update BitbucketConfigJsonContext**:
   - If adding new configuration types, add a `[JsonSerializable(typeof(YourNewType))]` attribute to the appropriate JsonContext class
   - This is required for source generation in AOT/trimmed applications

## Adding New MCP Tools

1. **Create a new partial class file**:
   - Create a new file in `src/BitbucketMcpTools/` named `PullRequestTools_YourToolName.cs`
   - Follow the naming convention of existing tool files
   - Follow namespace conventions of existing tool files
   - Place using statements in GlobalUsings.cs file of the project

2. **Implement the tool method**:
   ```csharp
   public partial class PullRequestTools
   {
       [McpServerTool(Name = "your_tool_name"),
        Description("Description of your tool")]
       public async Task<string> YourToolName(
           [Description("Description of parameter")] string parameterName)
       {
           await using (var scope = _serviceProvider.CreateAsyncScope())
           {
               var clientFactory = scope.ServiceProvider.GetRequiredService<IBitbucketClientFactory>();
               var clientResult = await clientFactory.CreateClientAsync(repoName);
               if (clientResult.IsFailed)
               {
                   return clientResult.Errors.First().ToString() ?? "Internal Error unknown error when creating Bitbucket client";
               }

               var bitBucketClient = clientResult.Value;

               if (bitBucketClient is null)
               {
                   return "Internal Error unknown error when creating Bitbucket client";
               }

               try
               {
                   // Implement your tool logic here
                   // ...

                   return "Your result in markdown format";
               }
               catch (Exception ex)
               {
                   return $"ERROR: Failed due to exception '{ex.Message}'";
               }
           }
       }
   }
   ```

3. **Error Handling Conventions**:
   - Prefer the use of FluentResult then using try/catch block
   - Return error messages with an "ERROR:" prefix
   - Include error codes for easier troubleshooting (e.g., "ERROR: (1234)")
   - Return results in markdown format

## Bitbucket Client Usage

1. **Creating a client**:
   - Always use the `IBitbucketClientFactory` from dependency injection
   - Create clients within a service scope using `_serviceProvider.CreateAsyncScope()`
   - Check for failures with `clientResult.IsFailed`

2. **Repository Selection**:
   - The `BitbucketRemoteMcpServer` supports multiple repositories
   - The repository name is passed as a parameter to tools

3. **API Documentation Reference**:
   - TBD: Bitbucket API documentation reference will be added here
   - **For Cline/AI Assistants**: When you need detailed information about available Bitbucket client methods, their signatures, parameters, or usage, reference the API documentation once available

## Logging Conventions

- Use Serilog for logging
- Log appropriate information at appropriate levels:
  - `LogDebug` for detailed troubleshooting
  - `LogInformation` for general operational information
  - `LogError` for errors that require attention

## Return Format Conventions

- Return results in markdown format
- For lists, use bullet points with `- item` syntax
- For pull requests, include headers with relevant information (ID, Title, Author, State, etc.)

## Input Validation

- Validate all input parameters at the beginning of the method
- Use descriptive error messages for invalid inputs
- Return early if validation fails

## Deployment

- **BitbucketMcpServer**: Deploy as a standalone executable
- **BitbucketRemoteMcpServer**: 
  - Can be deployed as a standalone web application
  - Container support can be enabled with the following properties in the project file:
    ```xml
    <EnableSdkContainerSupport>true</EnableSdkContainerSupport>
    <ContainerRepository>peakflames/bitbucket-remote-mcp-server</ContainerRepository>
    ```

## AI Assistant Guidelines (Cline Rules)

When working on this project as an AI assistant:

1. **Follow established patterns**:
   - Look at existing tool implementations in `src/BitbucketMcpTools/` for patterns
   - Use the same error handling, logging, and return format conventions
   - Follow the dependency injection patterns shown in existing code

2. **Validate your understanding**:
   - When implementing new tools, cross-reference existing implementations to ensure correct usage
   - Pay attention to async/await patterns and Result<T> return types

3. **Use build.py for all operations**:
   - PREFER to use `build.py` for nearly all build, test, and verification activities
   - Always verify changes using MCP tools before considering work complete

## C# Coding Conventions

- Use `var` for all variables
- Use curly braces for all blocks
- Prefer Global Using Statements over Local Using Statements
- Prefer FluentResults over null handling or Exceptions for error handling

## MCP Tool Attribute Syntax

- Use multi-line format for MCP tool attributes:
  ```csharp
  [McpServerTool(Name = "tool_name"),
   Description("Tool description")]
  ```
- Do NOT use `[McpServerTool(Name = "...", Description = "...")]` - Description must be a separate attribute on its own line
- Always check existing tool files for the exact syntax pattern

## Documentation Guidelines

- Do NOT create summary/setup/guide markdown files unless explicitly requested
- Complete the task and use attempt_completion to explain what was done
- Keep explanations concise in the attempt_completion result

## ⚠️ CRITICAL: appsettings.json Security Rule

**NEVER commit, add, reset, checkout, discard, or modify `src/BitbucketRemoteMcpServer/appsettings.json` in any git operation.**

This file contains sensitive credentials (usernames, passwords, API tokens, server URLs) that must be protected at all costs. The file should be treated as if it doesn't exist when performing any git operations:

- ❌ NEVER use `git add src/BitbucketRemoteMcpServer/appsettings.json`
- ❌ NEVER include it in commits
- ❌ NEVER stage changes to this file
- ❌ NEVER reset or checkout this file
- ❌ NEVER discard changes to this file through git

**During release processes and any git operations:**
- Always explicitly exclude this file from staging
- If git status shows it as modified, ignore it completely
- Only stage and commit the specific files needed for the task
- Use explicit file paths in `git add` commands rather than wildcards that might accidentally include it

**Violation of this rule could expose sensitive credentials and compromise security.**
