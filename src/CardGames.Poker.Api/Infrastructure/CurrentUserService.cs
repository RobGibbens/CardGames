using System.Security.Claims;

namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Implementation of <see cref="ICurrentUserService"/> that retrieves user information
/// from the current HTTP context's claims principal.
/// </summary>
public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
	private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

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

			return false;
		}
	}
}
