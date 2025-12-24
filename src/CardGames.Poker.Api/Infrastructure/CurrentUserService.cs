using System.Security.Claims;

namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Implementation of <see cref="ICurrentUserService"/> that retrieves user information
/// from the current HTTP context's claims principal or custom headers from Blazor frontend.
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
	private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;
	private HttpContext? HttpContext => httpContextAccessor.HttpContext;

	/// <inheritdoc />
	public string? UserId
	{
		get
		{
			// First try claims from JWT authentication
			var claimUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier)
							  ?? User?.FindFirstValue("oid")  // Azure AD Object ID
							  ?? User?.FindFirstValue("sub"); // JWT subject claim

			if (!string.IsNullOrWhiteSpace(claimUserId))
			{
				return claimUserId;
			}

			// Fallback to custom header from Blazor frontend
			if (HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var headerUserId) == true)
			{
				return headerUserId.ToString();
			}

			return null;
		}
	}

	/// <inheritdoc />
	public string? UserName
	{
		get
		{
			// First try claims from JWT authentication
			var claimUserName = User?.FindFirstValue(ClaimTypes.Email)
								?? User?.FindFirstValue("email")
								?? User?.FindFirstValue("preferred_username")
								?? User?.Identity?.Name;

			if (!string.IsNullOrWhiteSpace(claimUserName))
			{
				return claimUserName;
			}

			// Fallback to custom header from Blazor frontend
			if (HttpContext?.Request.Headers.TryGetValue("X-User-Name", out var headerUserName) == true)
			{
				return headerUserName.ToString();
			}

			return null;
		}
	}

	/// <inheritdoc />
	public bool IsAuthenticated
	{
		get
		{
			// Check JWT authentication first
			if (User?.Identity?.IsAuthenticated == true)
			{
				return true;
			}

			// Fallback to custom header from Blazor frontend
			if (HttpContext?.Request.Headers.TryGetValue("X-User-Authenticated", out var headerAuth) == true)
			{
				return string.Equals(headerAuth.ToString(), "true", StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}
	}
}
