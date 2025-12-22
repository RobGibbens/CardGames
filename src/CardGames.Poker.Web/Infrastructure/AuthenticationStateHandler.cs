using System.Security.Claims;

namespace CardGames.Poker.Web.Infrastructure;

/// <summary>
/// DelegatingHandler that adds user identity claims to outgoing HTTP requests.
/// This allows the backend API to identify the authenticated Blazor user.
/// </summary>
public class AuthenticationStateHandler(
    CircuitServicesAccessor circuitServicesAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var user = circuitServicesAccessor.User;

        if (user?.Identity is { IsAuthenticated: true })
        {
            // Add user identity claims as headers for the API to consume
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? user.FindFirstValue("sub");

            var userName = user.FindFirstValue(ClaimTypes.Email)
                           ?? user.FindFirstValue("email")
                           ?? user.FindFirstValue("preferred_username")
                           ?? user.Identity.Name;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                request.Headers.TryAddWithoutValidation("X-User-Id", userId);
            }

            if (!string.IsNullOrWhiteSpace(userName))
            {
                request.Headers.TryAddWithoutValidation("X-User-Name", userName);
            }

            // Add authentication indicator
            request.Headers.TryAddWithoutValidation("X-User-Authenticated", "true");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
