using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;

internal static class PhaseDescriptionResolver
{
	public static string? TryResolve(string? gameTypeCode, string? currentPhase)
	{
		if (string.IsNullOrWhiteSpace(currentPhase))
		{
			return null;
		}

		if (string.IsNullOrWhiteSpace(gameTypeCode))
		{
			return null;
		}

		return TryResolveEnumDescription<Phases>(currentPhase);
	}

	private static string? TryResolveEnumDescription<TEnum>(string currentPhase)
		where TEnum : struct, Enum
	{
		return Enum.TryParse<TEnum>(currentPhase, ignoreCase: true, out var parsed)
			? parsed.GetDescriptionOrName()
			: null;
	}
}