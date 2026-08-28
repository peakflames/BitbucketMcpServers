namespace BitbucketRemoteMcpServer.Broker;

/// <summary>
/// Returns Success immediately when Broker is disabled, same shape as McpAuthOptionsValidator and
/// AccessOptionsValidator, so a malformed Broker section can never break a server with this
/// feature off.
/// </summary>
public sealed class BrokerOptionsValidator : IValidateOptions<BrokerOptions>
{
    public ValidateOptionsResult Validate(string? name, BrokerOptions options)
    {
        if (!options.Enabled)
            return ValidateOptionsResult.Success;

        return string.IsNullOrWhiteSpace(options.DatabasePath)
            ? ValidateOptionsResult.Fail("Broker:DatabasePath must not be blank.")
            : ValidateOptionsResult.Success;
    }
}
