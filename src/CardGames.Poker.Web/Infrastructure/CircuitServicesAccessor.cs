using System.Security.Claims;

namespace CardGames.Poker.Web.Infrastructure;

/// <summary>
/// Provides access to the current user's claims principal from a different DI scope (e.g., DelegatingHandler).
/// This is needed because DelegatingHandler instances are created outside the Blazor circuit scope.
/// </summary>
public class CircuitServicesAccessor
{
    private static readonly AsyncLocal<ClaimsPrincipal?> _currentUser = new();

    /// <summary>
    /// Gets or sets the current user's claims principal.
    /// </summary>
    public ClaimsPrincipal? User
    {
        get => _currentUser.Value;
        set => _currentUser.Value = value;
    }
}
