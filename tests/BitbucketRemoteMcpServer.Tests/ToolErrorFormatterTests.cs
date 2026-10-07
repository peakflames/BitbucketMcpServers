using Microsoft.Extensions.Logging;

namespace BitbucketRemoteMcpServer.Tests;

public class ToolErrorFormatterTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                                Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    private static SharpBucket.V2.BitbucketV2Exception V2Exception(HttpStatusCode status, string message) =>
        new(status, new SharpBucket.V2.Pocos.ErrorResponse
        {
            type = "error",
            error = new SharpBucket.V2.Pocos.Error { message = message },
        });

    [Theory]
    [InlineData("Authorization: Bearer abc.DEF-123_xyz~+/=", "Authorization: Bearer [REDACTED]")]
    [InlineData("authorization: BEARER abc123", "authorization: BEARER [REDACTED]")]
    [InlineData("Authorization: Basic dXNlcjpwYXNz", "Authorization: Basic [REDACTED]")]
    public void Redact_ReplacesAuthSchemeValues(string input, string expected) =>
        Assert.Equal(expected, ToolErrorFormatter.Redact(input));

    [Fact]
    public void Redact_ReplacesPresignedQueryValues()
    {
        var input = "https://bucket.example.invalid/obj?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Credential=AKIAEXAMPLE%2F20260101&X-Amz-Signature=deadbeef";

        var redacted = ToolErrorFormatter.Redact(input);

        Assert.Equal(
            "https://bucket.example.invalid/obj?X-Amz-…=[REDACTED]&X-Amz-…=[REDACTED]&X-Amz-…=[REDACTED]",
            redacted);
    }

    [Fact]
    public void Redact_LeavesOrdinaryTextUntouched() =>
        Assert.Equal("Not found: fake-workspace/demo-repo", ToolErrorFormatter.Redact("Not found: fake-workspace/demo-repo"));

    [Fact]
    public void Format_BitbucketV2Exception_PassesThroughStatusAndMessage()
    {
        var text = ToolErrorFormatter.Format("get commit", V2Exception(HttpStatusCode.NotFound, "Not found: abc"));

        Assert.Equal("ERROR: Failed to get commit: NotFound Not found: abc", text);
    }

    [Fact]
    public void Format_BitbucketException_PassesThroughStatusAndMessage()
    {
        var ex = new SharpBucket.BitbucketException(HttpStatusCode.Redirect, "Refused to follow a redirect from HTTPS to HTTP.");

        var text = ToolErrorFormatter.Format("read file", ex);

        Assert.Equal("ERROR: Failed to read file: Found Refused to follow a redirect from HTTPS to HTTP.", text);
    }

    [Fact]
    public void Format_GenericException_ShowsOnlyTheTypeName()
    {
        var text = ToolErrorFormatter.Format("read file", new InvalidOperationException("Bearer super-secret-token"));

        Assert.Equal("ERROR: Failed to read file: InvalidOperationException", text);
    }

    [Fact]
    public void Format_DropsTheInnerException()
    {
        var ex = new SharpBucket.BitbucketException(HttpStatusCode.BadRequest, "outer", new InvalidOperationException("inner-detail-xyz"));

        var text = ToolErrorFormatter.Format("list branches", ex);

        Assert.DoesNotContain("inner-detail-xyz", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Inner Exception", text, StringComparison.Ordinal);
        Assert.Equal("ERROR: Failed to list branches: BadRequest outer", text);
    }

    [Fact]
    public void Format_BearerTokenInsideABitbucketMessage_IsRedacted()
    {
        var ex = V2Exception(HttpStatusCode.BadRequest,
            "<ArgumentValue>Bearer eyJhbGciOi.secret-payload.sig</ArgumentValue> https://s3.example.invalid/o?X-Amz-Signature=cafebabe");

        var text = ToolErrorFormatter.Format("read file", ex);

        Assert.DoesNotContain("eyJhbGciOi", text, StringComparison.Ordinal);
        Assert.DoesNotContain("cafebabe", text, StringComparison.Ordinal);
        Assert.Contains("Bearer [REDACTED]", text, StringComparison.Ordinal);
        Assert.Contains("X-Amz-…=[REDACTED]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_LogsARedactedWarning_NeverTheRawValue()
    {
        var logger = new CapturingLogger<RepositoryTools>();
        var ex = V2Exception(HttpStatusCode.BadRequest, "Bearer logged-secret-token");

        ToolErrorFormatter.Format("read file", ex, logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.DoesNotContain("logged-secret-token", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Bearer [REDACTED]", entry.Message, StringComparison.Ordinal);
    }
}
