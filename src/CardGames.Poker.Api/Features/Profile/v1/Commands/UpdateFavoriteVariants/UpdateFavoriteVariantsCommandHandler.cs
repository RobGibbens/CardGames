using System.Text.Json;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Profile.v1.Commands.UpdateFavoriteVariants;

public sealed class UpdateFavoriteVariantsCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<UpdateFavoriteVariantsCommand, OneOf<FavoriteVariantsDto, UpdateFavoriteVariantsError>>
{
	private const int DefaultSmallBlind = 1;
	private const int DefaultBigBlind = 2;
	private const int DefaultAnte = 5;
	private const int DefaultMinimumBet = 10;

	public async Task<OneOf<FavoriteVariantsDto, UpdateFavoriteVariantsError>> Handle(UpdateFavoriteVariantsCommand request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return new UpdateFavoriteVariantsError(UpdateFavoriteVariantsErrorCode.Unauthorized, "User is not authenticated.");
		}

		var normalizedFavoriteVariantCodes = NormalizeFavoriteVariantCodes(request.FavoriteVariantCodes);
		var userId = currentUserService.UserId;
		var now = DateTimeOffset.UtcNow;

		var preferences = await UpsertAsync(userId, normalizedFavoriteVariantCodes, now, cancellationToken);

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
			preferences = await UpsertAsync(resolvedUserId, normalizedFavoriteVariantCodes, now, cancellationToken);
			await context.SaveChangesAsync(cancellationToken);
		}

		return new FavoriteVariantsDto
		{
			FavoriteVariantCodes = normalizedFavoriteVariantCodes
		};
	}

	private async Task<UserGamePreferences> UpsertAsync(
		string userId,
		IReadOnlyList<string> normalizedFavoriteVariantCodes,
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
				DefaultSmallBlind = DefaultSmallBlind,
				DefaultBigBlind = DefaultBigBlind,
				DefaultAnte = DefaultAnte,
				DefaultMinimumBet = DefaultMinimumBet,
				FavoriteVariantCodesJson = SerializeFavoriteVariantCodes(normalizedFavoriteVariantCodes),
				CreatedAtUtc = now,
				UpdatedAtUtc = now
			};

			context.UserGamePreferences.Add(preferences);
		}
		else
		{
			preferences.FavoriteVariantCodesJson = SerializeFavoriteVariantCodes(normalizedFavoriteVariantCodes);
			preferences.UpdatedAtUtc = now;
		}

		return preferences;
	}

	private static IReadOnlyList<string> NormalizeFavoriteVariantCodes(IEnumerable<string>? favoriteVariantCodes)
	{
		if (favoriteVariantCodes is null)
		{
			return [];
		}

		return favoriteVariantCodes
			.Where(code => !string.IsNullOrWhiteSpace(code))
			.Select(code => code.Trim().ToUpperInvariant())
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
			.ToArray();
	}

	private static string SerializeFavoriteVariantCodes(IReadOnlyList<string> favoriteVariantCodes)
	{
		return JsonSerializer.Serialize(favoriteVariantCodes);
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