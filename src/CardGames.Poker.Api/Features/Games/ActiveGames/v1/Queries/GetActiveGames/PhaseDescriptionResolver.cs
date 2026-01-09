using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.FollowTheQueen;
using CardGames.Poker.Games.HoldEm;
using CardGames.Poker.Games.KingsAndLows;
using CardGames.Poker.Games.Omaha;
using CardGames.Poker.Games.SevenCardStud;
using CardGames.Poker.Games.TwosJacksManWithTheAxe;

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
			? ((Enum)(object)parsed).GetDescriptionOrName()
			: null;
	}
}
