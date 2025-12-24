using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace CardGames.Poker.Web.Infrastructure;

/// <summary>
/// Circuit handler that captures the current user's claims principal so it can be accessed
/// from outgoing HTTP request middleware (DelegatingHandler).
/// </summary>
internal sealed class CircuitServicesActivityHandler(
    AuthenticationStateProvider authenticationStateProvider,
    CircuitServicesAccessor accessor) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(
        Func<CircuitInboundActivityContext, Task> next)
    {
        return async context =>
        {
            var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
            accessor.User = authState.User;
            await next(context);
            accessor.User = null;
        };
    }
}
