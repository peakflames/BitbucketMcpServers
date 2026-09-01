namespace BitbucketRemoteMcpServer.Broker.Endpoints;

/// <summary>Maps the broker's own AS endpoints. Called only when <c>AddBroker()</c> returned
/// true — see <see cref="Program"/> — so none of these routes exist on a server that never
/// opted in.</summary>
public static class BrokerEndpointsExtensions
{
    public static void MapBrokerEndpoints(this WebApplication app)
    {
        app.MapGet("/.well-known/oauth-authorization-server", AuthorizationServerMetadataEndpoint.Handle);
        app.MapGet("/.well-known/jwks.json", JwksEndpoint.Handle);
        app.MapGet("/authorize", AuthorizeEndpoint.Handle);
        app.MapGet("/oauth/callback", CallbackEndpoint.HandleAsync);
        app.MapPost("/token", TokenEndpoint.HandleAsync);

        var broker = app.Services.GetRequiredService<IOptions<BrokerOptions>>().Value;
        if (broker.DcrEnabled)
        {
            app.MapPost("/register", RegisterEndpoint.HandleAsync);
        }
    }
}
