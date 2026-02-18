using CardGames.Poker.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Leagues.v1.Queries;

internal static class LeagueUserDisplayNameResolver
{
	public static async Task<IReadOnlyDictionary<string, string>> GetDisplayNamesByUserIdAsync(
		CardsDbContext context,
		IEnumerable<string> userIds,
		CancellationToken cancellationToken)
	{
		var distinctUserIds = userIds
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.Distinct(StringComparer.Ordinal)
			.ToArray();

		if (distinctUserIds.Length == 0)
		{
			return new Dictionary<string, string>(StringComparer.Ordinal);
		}

		var users = await context.Users
			.AsNoTracking()
			.Where(u => distinctUserIds.Contains(u.Id))
			.Select(u => new
			{
				u.Id,
				u.FirstName,
				u.LastName,
				u.UserName
			})
			.ToListAsync(cancellationToken);

		return users.ToDictionary(
			u => u.Id,
			u => BuildDisplayName(u.FirstName, u.LastName, u.UserName, u.Id),
			StringComparer.Ordinal);
	}

	public static string GetDisplayNameOrFallback(IReadOnlyDictionary<string, string> displayNamesByUserId, string userId)
	{
		if (!string.IsNullOrWhiteSpace(userId) && displayNamesByUserId.TryGetValue(userId, out var displayName) && !string.IsNullOrWhiteSpace(displayName))
		{
			return displayName;
		}

		return userId;
	}

	private static string BuildDisplayName(string? firstName, string? lastName, string? userName, string fallbackUserId)
	{
		var trimmedFirstName = string.IsNullOrWhiteSpace(firstName) ? null : firstName.Trim();
		var trimmedLastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();

		if (!string.IsNullOrWhiteSpace(trimmedFirstName) && !string.IsNullOrWhiteSpace(trimmedLastName))
		{
			return $"{trimmedFirstName} {trimmedLastName}";
		}

		if (!string.IsNullOrWhiteSpace(trimmedFirstName))
		{
			return trimmedFirstName;
		}

		if (!string.IsNullOrWhiteSpace(trimmedLastName))
		{
			return trimmedLastName;
		}

		if (!string.IsNullOrWhiteSpace(userName))
		{
			return userName.Trim();
		}

		return fallbackUserId;
	}
}
