using System.Text.Json;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Profile.v1.Queries.GetFavoriteVariants;

public sealed class GetFavoriteVariantsQueryHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<GetFavoriteVariantsQuery, FavoriteVariantsDto>
{
	public async Task<FavoriteVariantsDto> Handle(GetFavoriteVariantsQuery request, CancellationToken cancellationToken)
	{
		if (!currentUserService.IsAuthenticated || string.IsNullOrWhiteSpace(currentUserService.UserId))
		{
			return EmptyFavorites();
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
			return EmptyFavorites();
		}

		return new FavoriteVariantsDto
		{
			FavoriteVariantCodes = DeserializeFavoriteVariantCodes(preferences.FavoriteVariantCodesJson)
		};
	}

	private static FavoriteVariantsDto EmptyFavorites() => new()
	{
		FavoriteVariantCodes = []
	};

	private static IReadOnlyList<string> DeserializeFavoriteVariantCodes(string? favoriteVariantCodesJson)
	{
		if (string.IsNullOrWhiteSpace(favoriteVariantCodesJson))
		{
			return [];
		}

		try
		{
			return NormalizeFavoriteVariantCodes(JsonSerializer.Deserialize<string[]>(favoriteVariantCodesJson));
		}
		catch (JsonException)
		{
			return [];
		}
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