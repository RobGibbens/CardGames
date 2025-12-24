using CardGames.Poker.Games.FiveCardDraw;
using CardGames.Poker.Games.FollowTheQueen;
using CardGames.Poker.Games.HoldEm;
using CardGames.Poker.Games.KingsAndLows;
using CardGames.Poker.Games.Omaha;
using CardGames.Poker.Games.SevenCardStud;

namespace CardGames.Poker.Api.Games;

/// <summary>
/// Resolves a persisted <see cref="Data.Entities.Game.CurrentPhase"/> string back into the correct
/// per-variant phase enum value.
/// </summary>
public static class PokerGamePhaseRegistry
{
	/// <summary>
	/// Attempts to parse <paramref name="currentPhase"/> into the correct phase enum, based on <paramref name="gameTypeCode"/>.
	/// </summary>
	public static bool TryResolve(string? gameTypeCode, string? currentPhase, out Enum? phase)
	{
		if (string.IsNullOrWhiteSpace(gameTypeCode) || string.IsNullOrWhiteSpace(currentPhase))
		{
			phase = null;
			return false;
		}

		switch (gameTypeCode.ToUpperInvariant())
		{
			case PokerGameMetadataRegistry.FiveCardDrawCode:
				return TryResolveEnum<FiveCardDrawPhase>(currentPhase, out phase);
			case PokerGameMetadataRegistry.HoldEmCode:
				return TryResolveEnum<HoldEmPhase>(currentPhase, out phase);
			case PokerGameMetadataRegistry.OmahaCode:
				return TryResolveEnum<OmahaPhase>(currentPhase, out phase);
			case PokerGameMetadataRegistry.SevenCardStudCode:
				return TryResolveEnum<SevenCardStudPhase>(currentPhase, out phase);
			case PokerGameMetadataRegistry.KingsAndLowsCode:
				return TryResolveEnum<KingsAndLowsPhase>(currentPhase, out phase);
			case PokerGameMetadataRegistry.FollowTheQueenCode:
				return TryResolveEnum<FollowTheQueenPhase>(currentPhase, out phase);
			default:
				phase = null;
				return false;
		}
	}

	/// <summary>
	/// Resolves <paramref name="currentPhase"/> into a strongly typed enum for the specified game type.
	/// </summary>
	public static bool TryResolve<TEnum>(string? gameTypeCode, string? currentPhase, out TEnum phase)
		where TEnum : struct, Enum
	{
		phase = default;

		if (!TryResolve(gameTypeCode, currentPhase, out var boxed) || boxed is not TEnum typed)
		{
			return false;
		}

		phase = typed;
		return true;
	}

	private static bool TryResolveEnum<TEnum>(string currentPhase, out Enum? phase)
		where TEnum : struct, Enum
	{
		if (!Enum.TryParse<TEnum>(currentPhase, ignoreCase: true, out var parsed))
		{
			phase = null;
			return false;
		}

		phase = (Enum)(object)parsed;
		return true;
	}
}
