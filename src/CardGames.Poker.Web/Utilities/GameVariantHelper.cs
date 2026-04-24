using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Web.Utilities;

public static class GameVariantHelper
{
	public const string DealersChoiceCode = "DEALERSCHOICE";
	public const string ScrewYourNeighborCode = "SCREWYOURNEIGHBOR";
	public const string InBetweenCode = "INBETWEEN";

	private static readonly HashSet<string> BlindGameCodes = new(StringComparer.OrdinalIgnoreCase)
	{
		"BOBBARKER",
		"CRAZYPINEAPPLE",
		"HOLDEM",
		"HOLDTHEBASEBALL",
		"IRISHHOLDEM",
		"KLONDIKE",
		"NEBRASKA",
		"OMAHA",
		"PHILSMOM",
		"REDRIVER",
		"SOUTHDAKOTA"
	};

	public static bool HasBlinds(GetAvailablePokerGamesResponse? game, string? fallbackGameCode = null)
	{
		if (game is not null)
		{
			if (IsDealersChoice(game.Code) || IsScrewYourNeighbor(game.Code))
			{
				return false;
			}

			return game.VariantType == VariantType.HoldEm;
		}

		return BlindGameCodes.Contains(NormalizeGameCode(fallbackGameCode));
	}

	public static bool IsDealersChoice(string? gameCode)
	{
		return string.Equals(gameCode, DealersChoiceCode, StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsScrewYourNeighbor(string? gameCode)
	{
		return string.Equals(gameCode, ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase);
	}

	public static bool IsInBetween(string? gameCode)
	{
		return string.Equals(gameCode, InBetweenCode, StringComparison.OrdinalIgnoreCase);
	}

	public static bool ShowsMinimumBet(string? gameCode)
	{
		return !IsScrewYourNeighbor(gameCode) && !IsInBetween(gameCode);
	}

	private static string NormalizeGameCode(string? value)
	{
		return string.IsNullOrWhiteSpace(value)
			? string.Empty
			: value.Trim().ToUpperInvariant();
	}
}