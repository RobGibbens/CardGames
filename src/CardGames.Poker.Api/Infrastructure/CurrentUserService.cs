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
			var claimUserId = User?.FindFirstValue(ClaimTypes.NameIdentifier)
				?? User?.FindFirstValue("oid")
				?? User?.FindFirstValue("sub");

			if (!string.IsNullOrWhiteSpace(claimUserId))
			{
				return claimUserId;
			}

			if (HttpContext?.Request.Headers.TryGetValue("X-User-Id", out var headerUserId) == true)
			{
				var value = headerUserId.ToString();
				return string.IsNullOrWhiteSpace(value) ? null : value;
			}

			return null;
		}
	}

	/// <inheritdoc />
	public string? UserName
	{
		get
		{
			var claimUserName = User?.FindFirstValue(ClaimTypes.Email)
				?? User?.FindFirstValue("email")
				?? User?.FindFirstValue("preferred_username")
				?? User?.Identity?.Name;

			if (!string.IsNullOrWhiteSpace(claimUserName))
			{
				return claimUserName;
			}

			if (HttpContext?.Request.Headers.TryGetValue("X-User-Name", out var headerUserName) == true)
			{
				var value = headerUserName.ToString();
				return string.IsNullOrWhiteSpace(value) ? null : value;
			}

			return null;
		}
	}

	/// <inheritdoc />
	public string? UserEmail
	{
		get
		{
			var claimEmail = User?.FindFirstValue(ClaimTypes.Email)
				?? User?.FindFirstValue("email");

			if (!string.IsNullOrWhiteSpace(claimEmail))
			{
				return claimEmail;
			}

			if (HttpContext?.Request.Headers.TryGetValue("X-User-Email", out var headerEmail) == true)
			{
				var value = headerEmail.ToString();
				return string.IsNullOrWhiteSpace(value) ? null : value;
			}

			return null;
		}
	}

	/// <inheritdoc />
	public bool IsAuthenticated
	{
		get
		{
			if (User?.Identity?.IsAuthenticated == true)
			{
				return true;
			}

			if (HttpContext?.Request.Headers.TryGetValue("X-User-Authenticated", out var headerAuth) == true)
			{
				return string.Equals(headerAuth.ToString(), "true", StringComparison.OrdinalIgnoreCase);
			}

			return false;
		}
	}
}
