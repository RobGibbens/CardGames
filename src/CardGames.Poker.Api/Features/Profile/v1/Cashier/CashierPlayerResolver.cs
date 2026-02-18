using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.Cashier;

internal static class CashierPlayerResolver
{
	public static async Task<Player?> TryResolveAsync(
		CardsDbContext context,
		ICurrentUserService currentUserService,
		CancellationToken cancellationToken)
	{
		var userEmail = currentUserService.UserEmail;
		var userId = currentUserService.UserId;
		var userName = currentUserService.UserName;

		if (!string.IsNullOrWhiteSpace(userEmail))
		{
			var byEmail = await context.Players
				.FirstOrDefaultAsync(p => p.Email == userEmail, cancellationToken);

			if (byEmail is not null)
			{
				return byEmail;
			}
		}

		if (!string.IsNullOrWhiteSpace(userId))
		{
			var byExternalId = await context.Players
				.FirstOrDefaultAsync(p => p.ExternalId == userId, cancellationToken);

			if (byExternalId is not null)
			{
				return byExternalId;
			}
		}

		if (!string.IsNullOrWhiteSpace(userName))
		{
			return await context.Players
				.FirstOrDefaultAsync(
					p => p.Name == userName || p.Email == userName,
					cancellationToken);
		}

		return null;
	}

	public static async Task<Player?> ResolveOrCreateAsync(
		CardsDbContext context,
		ICurrentUserService currentUserService,
		CancellationToken cancellationToken)
	{
		var existingPlayer = await TryResolveAsync(context, currentUserService, cancellationToken);
		if (existingPlayer is not null)
		{
			return existingPlayer;
		}

		if (!currentUserService.IsAuthenticated)
		{
			return null;
		}

		var displayName = currentUserService.UserName;
		var email = currentUserService.UserEmail;

		if (string.IsNullOrWhiteSpace(displayName) && string.IsNullOrWhiteSpace(email))
		{
			return null;
		}

		var now = DateTimeOffset.UtcNow;
		var player = new Player
		{
			Id = Guid.CreateVersion7(),
			Name = !string.IsNullOrWhiteSpace(displayName) ? displayName : email!,
			Email = email,
			ExternalId = currentUserService.UserId,
			CreatedAt = now,
			UpdatedAt = now,
			LastActiveAt = now,
			IsActive = true
		};

		context.Players.Add(player);
		return player;
	}
}
