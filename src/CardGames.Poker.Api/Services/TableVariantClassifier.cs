using CardGames.Poker.Api.Games;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Centralizes game-variant classification used while projecting table state.
/// Each predicate maps a stored game-type code to a known poker variant (or family of variants),
/// keeping variant branching out of the larger state-assembly flow.
/// </summary>
internal static class TableVariantClassifier
{
	internal static readonly StringComparer GameCodeComparer = StringComparer.OrdinalIgnoreCase;

	private static readonly HashSet<string> StudGameCodes =
		new(GameCodeComparer)
		{
			PokerGameMetadataRegistry.SevenCardStudCode,
			PokerGameMetadataRegistry.RazzCode,
			PokerGameMetadataRegistry.BaseballCode,
			PokerGameMetadataRegistry.FollowTheQueenCode,
			PokerGameMetadataRegistry.PairPressureCode,
			PokerGameMetadataRegistry.TollboothCode
		};

	internal static bool IsStudGame(string? gameTypeCode)
		=> !string.IsNullOrWhiteSpace(gameTypeCode) && StudGameCodes.Contains(gameTypeCode);

	internal static bool IsBaseballGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.BaseballCode);

	internal static bool IsKingsAndLowsGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.KingsAndLowsCode);

	internal static bool IsFollowTheQueenGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.FollowTheQueenCode);

	internal static bool IsPairPressureGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.PairPressureCode);

	internal static bool IsTwosJacksAxeGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.TwosJacksManWithTheAxeCode);

	internal static bool IsGoodBadUglyGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.GoodBadUglyCode);

	internal static bool IsHoldEmGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.HoldEmCode)
		   || IsGameType(gameTypeCode, PokerGameMetadataRegistry.RedRiverCode)
		   || IsGameType(gameTypeCode, PokerGameMetadataRegistry.KlondikeCode);

	internal static bool IsHoldTheBaseballGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.HoldTheBaseballCode);

	internal static bool IsOmahaGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.OmahaCode);

	internal static bool IsNebraskaGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.NebraskaCode);

	internal static bool IsSouthDakotaGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.SouthDakotaCode);

	internal static bool IsIrishHoldEmGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.IrishHoldEmCode)
		   || IsGameType(gameTypeCode, PokerGameMetadataRegistry.PhilsMomCode)
		   || IsGameType(gameTypeCode, PokerGameMetadataRegistry.CrazyPineappleCode);

	internal static bool IsBobBarkerGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.BobBarkerCode);

	internal static bool IsScrewYourNeighborGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode);

	internal static bool IsInBetweenGame(string? gameTypeCode)
		=> IsGameType(gameTypeCode, PokerGameMetadataRegistry.InBetweenCode);

	internal static bool IsGameType(string? gameTypeCode, string expectedCode)
		=> !string.IsNullOrWhiteSpace(gameTypeCode) &&
		   string.Equals(gameTypeCode, expectedCode, StringComparison.OrdinalIgnoreCase);
}
