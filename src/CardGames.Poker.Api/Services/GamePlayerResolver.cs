using System.Security.Claims;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Services;

/// <summary>
///     Matches the signed-in user to a seat using the same email, name, and external-id rules
///     as leave-game and rabbit hunt.
/// </summary>
public sealed class GamePlayerResolver(CardsDbContext context, ICurrentUserService currentUserService) : IGamePlayerResolver
{
	public Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated)
			return Task.FromResult<GamePlayer?>(null);

		return FindSeatedPlayerAsync(gameId, currentUserService.UserEmail, currentUserService.UserName,
			currentUserService.UserId, cancellationToken);
	}

	public Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, ClaimsPrincipal user,
		CancellationToken cancellationToken)
	{
		if (user.Identity?.IsAuthenticated != true)
			return Task.FromResult<GamePlayer?>(null);

		var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
		var userEmail = user.FindFirstValue(ClaimTypes.Email) ?? user.FindFirstValue("email");
		var userName = userEmail
		               ?? user.FindFirstValue("preferred_username")
		               ?? user.FindFirstValue(ClaimTypes.Name)
		               ?? user.Identity?.Name;

		return FindSeatedPlayerAsync(gameId, userEmail, userName, userId, cancellationToken);
	}

	private Task<GamePlayer?> FindSeatedPlayerAsync(Guid gameId, string? userEmail, string? userName, string? userId,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(userEmail) &&
		    string.IsNullOrWhiteSpace(userName) &&
		    string.IsNullOrWhiteSpace(userId))
			return Task.FromResult<GamePlayer?>(null);

		return context.GamePlayers
			.Where(gp => gp.GameId == gameId && gp.Status != GamePlayerStatus.Left)
			.Include(gp => gp.Player)
			.FirstOrDefaultAsync(gp =>
					(!string.IsNullOrWhiteSpace(userEmail) && gp.Player.Email == userEmail) ||
					(!string.IsNullOrWhiteSpace(userName) && gp.Player.Name == userName) ||
					(!string.IsNullOrWhiteSpace(userId) && gp.Player.ExternalId == userId),
				cancellationToken);
	}

	public async Task<bool> IsTableHostAsync(Guid gameId, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated)
			return false;

		var game = await context.Games
			.AsNoTracking()
			.Where(g => g.Id == gameId)
			.Select(g => new { g.CreatedById, g.CreatedByName })
			.FirstOrDefaultAsync(cancellationToken);

		if (game is null)
			return false;

		var userId = currentUserService.UserId;
		if (!string.IsNullOrWhiteSpace(game.CreatedById) &&
		    !string.IsNullOrWhiteSpace(userId) &&
		    string.Equals(game.CreatedById, userId, StringComparison.OrdinalIgnoreCase))
			return true;

		var userName = currentUserService.UserName;
		var userEmail = currentUserService.UserEmail;
		if (string.IsNullOrWhiteSpace(game.CreatedByName))
			return false;

		return string.Equals(game.CreatedByName, userName, StringComparison.OrdinalIgnoreCase) ||
		       string.Equals(game.CreatedByName, userEmail, StringComparison.OrdinalIgnoreCase);
	}
}
