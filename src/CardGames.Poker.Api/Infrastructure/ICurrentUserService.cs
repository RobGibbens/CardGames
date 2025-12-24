namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
/// Service for accessing information about the currently authenticated user.
/// </summary>
public interface ICurrentUserService
{
	/// <summary>
	/// Gets the unique identifier of the currently authenticated user.
	/// </summary>
	/// <returns>The user's unique ID, or null if not authenticated.</returns>
	string? UserId { get; }

	/// <summary>
	/// Gets the name or email of the currently authenticated user.
	/// </summary>
	/// <returns>The user's name or email, or null if not authenticated.</returns>
	string? UserName { get; }

	/// <summary>
	/// Gets the email address of the currently authenticated user.
	/// </summary>
	/// <returns>The user's email, or null if not available.</returns>
	string? UserEmail { get; }

	/// <summary>
	/// Gets a value indicating whether the user is authenticated.
	/// </summary>
	bool IsAuthenticated { get; }
}
