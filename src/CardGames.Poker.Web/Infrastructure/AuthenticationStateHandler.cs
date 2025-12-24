using System.Security.Claims;

namespace CardGames.Poker.Web.Infrastructure;

/// <summary>
/// DelegatingHandler that adds user identity claims to outgoing HTTP requests.
/// This allows the backend API to identify the authenticated Blazor user.
/// Supports both local Identity accounts and external providers (Google, etc.).
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
            // Support both local Identity and external providers (Google, etc.)
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? user.FindFirstValue("sub");

            // For external providers like Google, try multiple claim types for email/name
            var userEmail = user.FindFirstValue(ClaimTypes.Email)
                           ?? user.FindFirstValue("email");

            var userName = userEmail
                           ?? user.FindFirstValue("preferred_username")
                           ?? user.Identity.Name;

            // Get the authentication provider (local vs external like Google)
            var authProvider = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/identityprovider")
                               ?? user.FindFirstValue("idp")
                               ?? "local";

            // Get name claim for display purposes (Google provides full name)
            var displayName = user.FindFirstValue(ClaimTypes.Name)
                              ?? user.FindFirstValue("name")
                              ?? userName;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                request.Headers.TryAddWithoutValidation("X-User-Id", userId);
            }

            if (!string.IsNullOrWhiteSpace(userName))
            {
                request.Headers.TryAddWithoutValidation("X-User-Name", userName);
            }

            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                request.Headers.TryAddWithoutValidation("X-User-Email", userEmail);
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                request.Headers.TryAddWithoutValidation("X-User-DisplayName", displayName);
            }

            // Add authentication provider indicator
            request.Headers.TryAddWithoutValidation("X-Auth-Provider", authProvider);

            // Add authentication indicator
            request.Headers.TryAddWithoutValidation("X-User-Authenticated", "true");
        }

        return base.SendAsync(request, cancellationToken);
    }
}
