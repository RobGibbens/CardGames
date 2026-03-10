using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetGamePreferences;

public sealed class GetGamePreferencesQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetGamePreferencesQuery, GamePreferencesDto>
{
	public async Task<GamePreferencesDto> Handle(GetGamePreferencesQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return DefaultPreferences();
		}

		var userId = currentUserService.UserId;

		var preferences = await context.UserGamePreferences
			.AsNoTracking()
			.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

		if (preferences is null)
		{
			var resolvedUserId = await ResolveLocalUserIdAsync(cancellationToken);
			if (!string.IsNullOrWhiteSpace(resolvedUserId)
				&& !string.Equals(resolvedUserId, userId, StringComparison.Ordinal))
			{
				preferences = await context.UserGamePreferences
					.AsNoTracking()
					.FirstOrDefaultAsync(x => x.UserId == resolvedUserId, cancellationToken);
			}
		}

		if (preferences is null)
		{
			return DefaultPreferences();
		}

		return new GamePreferencesDto
		{
			DefaultSmallBlind = preferences.DefaultSmallBlind,
			DefaultBigBlind = preferences.DefaultBigBlind,
			DefaultAnte = preferences.DefaultAnte,
			DefaultMinimumBet = preferences.DefaultMinimumBet
		};
	}

	private static GamePreferencesDto DefaultPreferences() => new()
	{
		DefaultSmallBlind = 1,
		DefaultBigBlind = 2,
		DefaultAnte = 5,
		DefaultMinimumBet = 10
	};

	private async Task<string?> ResolveLocalUserIdAsync(CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(currentUserService.UserEmail))
		{
			var normalizedEmail = currentUserService.UserEmail.ToUpperInvariant();
			var byEmail = await context.Users
				.AsNoTracking()
				.Where(u => u.NormalizedEmail == normalizedEmail || u.Email == currentUserService.UserEmail)
				.Select(u => u.Id)
				.FirstOrDefaultAsync(cancellationToken);

			if (!string.IsNullOrWhiteSpace(byEmail))
			{
				return byEmail;
			}
		}

		if (!string.IsNullOrWhiteSpace(currentUserService.UserName))
		{
			var normalizedUserName = currentUserService.UserName.ToUpperInvariant();
			var byUserName = await context.Users
				.AsNoTracking()
				.Where(u => u.NormalizedUserName == normalizedUserName || u.UserName == currentUserService.UserName)
				.Select(u => u.Id)
				.FirstOrDefaultAsync(cancellationToken);

			if (!string.IsNullOrWhiteSpace(byUserName))
			{
				return byUserName;
			}
		}

		return null;
	}
}
