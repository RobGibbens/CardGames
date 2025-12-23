using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Authentication handler that accepts user identity via custom headers.
/// Used for SignalR connections from Blazor frontend that uses cookie-based Identity.
/// </summary>
public sealed class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// The authentication scheme name for header-based authentication.
    /// </summary>
    public const string SchemeName = "HeaderAuth";

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderAuthenticationHandler"/> class.
    /// </summary>
    public HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for custom headers from Blazor frontend
        var hasUserId = Request.Headers.TryGetValue("X-User-Id", out var userIdValue);
        var hasUserName = Request.Headers.TryGetValue("X-User-Name", out var userNameValue);
        var hasAuthIndicator = Request.Headers.TryGetValue("X-User-Authenticated", out var authValue);

        // Check query string for SignalR negotiate (headers may be in query for WebSocket upgrade)
        if (!hasUserId && Request.Query.TryGetValue("userId", out var queryUserId))
        {
            userIdValue = queryUserId;
            hasUserId = true;
        }

        if (!hasUserName && Request.Query.TryGetValue("userName", out var queryUserName))
        {
            userNameValue = queryUserName;
            hasUserName = true;
        }

        if (!hasAuthIndicator && Request.Query.TryGetValue("authenticated", out var queryAuth))
        {
            authValue = queryAuth;
            hasAuthIndicator = true;
        }

        // Require at least user ID or authenticated indicator
        if (!hasUserId && !hasAuthIndicator)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValue.ToString();
        var userName = userNameValue.ToString();

        if (string.IsNullOrWhiteSpace(userId) && string.IsNullOrWhiteSpace(userName))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Build claims identity
        var claims = new List<Claim>();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        if (!string.IsNullOrWhiteSpace(userName))
        {
            claims.Add(new Claim(ClaimTypes.Name, userName));
            claims.Add(new Claim(ClaimTypes.Email, userName));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
