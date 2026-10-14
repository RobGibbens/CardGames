using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.Razz;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
///     Razz uses Seven Card Stud streets. The bring-in is the highest door, ace low,
///     and later streets act from the best ace-to-five board.
/// </summary>
public sealed class RazzFlowHandler : SevenCardStudFlowHandler
{
	public override string GameTypeCode => "RAZZ";

	protected override bool UsesRazzLowActionOrder => true;

	public override GameRules GetGameRules()
	{
		return RazzRules.CreateGameRules();
	}
}