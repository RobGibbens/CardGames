using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateGamePreferences;

public sealed class UpdateGamePreferencesCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<UpdateGamePreferencesCommand, OneOf<GamePreferencesDto, UpdateGamePreferencesError>>
{
	public async Task<OneOf<GamePreferencesDto, UpdateGamePreferencesError>> Handle(UpdateGamePreferencesCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new UpdateGamePreferencesError(UpdateGamePreferencesErrorCode.Unauthorized, "User is not authenticated.");
		}

		if (request.DefaultSmallBlind < 0
			|| request.DefaultBigBlind < 0
			|| request.DefaultAnte < 0
			|| request.DefaultMinimumBet < 0)
		{
			return new UpdateGamePreferencesError(UpdateGamePreferencesErrorCode.InvalidPreferences, "Preference values must be greater than or equal to 0.");
		}

		if (request.DefaultBigBlind < request.DefaultSmallBlind)
		{
			return new UpdateGamePreferencesError(UpdateGamePreferencesErrorCode.InvalidPreferences, "Big blind must be greater than or equal to small blind.");
		}

		var userId = currentUserService.UserId;
		var now = DateTimeOffset.UtcNow;

		var preferences = await UpsertAsync(userId, request, now, cancellationToken);

		try
		{
			await context.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException)
		{
			var resolvedUserId = await ResolveLocalUserIdAsync(cancellationToken);
			if (string.IsNullOrWhiteSpace(resolvedUserId)
				|| string.Equals(resolvedUserId, userId, StringComparison.Ordinal))
			{
				throw;
			}

			context.ChangeTracker.Clear();
			preferences = await UpsertAsync(resolvedUserId, request, now, cancellationToken);
			await context.SaveChangesAsync(cancellationToken);
		}

		return new GamePreferencesDto
		{
			DefaultSmallBlind = preferences.DefaultSmallBlind,
			DefaultBigBlind = preferences.DefaultBigBlind,
			DefaultAnte = preferences.DefaultAnte,
			DefaultMinimumBet = preferences.DefaultMinimumBet
		};
	}

	private async Task<UserGamePreferences> UpsertAsync(
		string userId,
		UpdateGamePreferencesCommand request,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var preferences = await context.UserGamePreferences
			.FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

		if (preferences is null)
		{
			preferences = new UserGamePreferences
			{
				UserId = userId,
				DefaultSmallBlind = request.DefaultSmallBlind,
				DefaultBigBlind = request.DefaultBigBlind,
				DefaultAnte = request.DefaultAnte,
				DefaultMinimumBet = request.DefaultMinimumBet,
				CreatedAtUtc = now,
				UpdatedAtUtc = now
			};

			context.UserGamePreferences.Add(preferences);
		}
		else
		{
			preferences.DefaultSmallBlind = request.DefaultSmallBlind;
			preferences.DefaultBigBlind = request.DefaultBigBlind;
			preferences.DefaultAnte = request.DefaultAnte;
			preferences.DefaultMinimumBet = request.DefaultMinimumBet;
			preferences.UpdatedAtUtc = now;
		}

		return preferences;
	}

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
