using CardGames.Poker.Api.Games;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Api.Features.Leagues.v1;

internal static class LeagueEventGameSettings
{
	private static (int? SmallBlind, int? BigBlind) ResolveBlindLevels(int? minBet, int? smallBlind, int? bigBlind)
	{
		int? resolvedBigBlind = bigBlind.HasValue && bigBlind.Value > 0
			? bigBlind.Value
			: minBet.HasValue && minBet.Value > 0
				? minBet.Value
				: null;

		int? resolvedSmallBlind = smallBlind.HasValue && smallBlind.Value > 0
			? smallBlind.Value
			: resolvedBigBlind.HasValue && resolvedBigBlind.Value > 1
				? Math.Max(1, resolvedBigBlind.Value / 2)
				: null;

		return (resolvedSmallBlind, resolvedBigBlind);
	}

	public static bool TryResolve(
		string? gameTypeCode,
		out string normalizedGameTypeCode,
		out GameRules? rules,
		out string? errorMessage)
	{
		normalizedGameTypeCode = string.Empty;
		rules = null;
		errorMessage = null;

		if (string.IsNullOrWhiteSpace(gameTypeCode))
		{
			errorMessage = "Game variant is required.";
			return false;
		}

		normalizedGameTypeCode = gameTypeCode.Trim().ToUpperInvariant();
		if (!PokerGameRulesRegistry.TryGet(normalizedGameTypeCode, out rules) || rules is null)
		{
			errorMessage = $"Unknown game code '{normalizedGameTypeCode}'.";
			return false;
		}

		return true;
	}

	public static string? Validate(GameRules rules, int? ante, int? minBet, int? smallBlind, int? bigBlind)
	{
		if (rules.Betting.HasBlinds)
		{
			var resolvedBlinds = ResolveBlindLevels(minBet, smallBlind, bigBlind);

			if (!resolvedBlinds.SmallBlind.HasValue || resolvedBlinds.SmallBlind.Value <= 0)
			{
				return "Small blind must be greater than 0 for blind-based games.";
			}

			if (!resolvedBlinds.BigBlind.HasValue || resolvedBlinds.BigBlind.Value <= 0)
			{
				return "Big blind must be greater than 0 for blind-based games.";
			}

			if (resolvedBlinds.BigBlind.Value <= resolvedBlinds.SmallBlind.Value)
			{
				return "Big blind must be greater than small blind.";
			}

			return null;
		}

		if (rules.Betting.HasAntes)
		{
			if (!ante.HasValue || ante.Value <= 0)
			{
				return "Ante must be greater than 0 for ante-based games.";
			}

			if (!minBet.HasValue || minBet.Value <= 0)
			{
				return "Minimum bet must be greater than 0 for ante-based games.";
			}
		}

		return null;
	}

	public static int NormalizeAnte(GameRules rules, int? ante)
	{
		return rules.Betting.HasAntes ? ante ?? 0 : 0;
	}

	public static int NormalizeMinBet(GameRules rules, int? minBet, int? bigBlind)
	{
		if (!rules.Betting.HasBlinds)
		{
			return minBet ?? 0;
		}

		return ResolveBlindLevels(minBet, null, bigBlind).BigBlind ?? 0;
	}

	public static int? NormalizeSmallBlind(GameRules rules, int? smallBlind)
	{
		return rules.Betting.HasBlinds ? smallBlind : null;
	}

	public static int? NormalizeBigBlind(GameRules rules, int? bigBlind)
	{
		return rules.Betting.HasBlinds ? bigBlind : null;
	}

	public static int? NormalizeSmallBlind(GameRules rules, int? minBet, int? smallBlind, int? bigBlind)
	{
		if (!rules.Betting.HasBlinds)
		{
			return null;
		}

		return ResolveBlindLevels(minBet, smallBlind, bigBlind).SmallBlind;
	}

	public static int? NormalizeBigBlind(GameRules rules, int? minBet, int? bigBlind)
	{
		if (!rules.Betting.HasBlinds)
		{
			return null;
		}

		return ResolveBlindLevels(minBet, null, bigBlind).BigBlind;
	}
}