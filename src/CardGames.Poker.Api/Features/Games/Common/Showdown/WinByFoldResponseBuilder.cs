namespace CardGames.Poker.Api.Features.Games.Common.Showdown;

/// <summary>
///     Builds win-by-fold showdown payloads that omit private hole cards.
/// </summary>
internal static class WinByFoldResponseBuilder
{
	/// <summary>
	///     Cards returned when the hand is awarded because everyone else folded.
	///     Always empty so hidden hole cards are not serialized to clients.
	/// </summary>
	internal static List<TCard> HiddenCards<TCard>() => [];
}
