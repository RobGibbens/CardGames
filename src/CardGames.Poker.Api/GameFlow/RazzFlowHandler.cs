using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.Razz;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Razz uses the same street/dealing behavior as Seven Card Stud.
/// Only game code and rules metadata differ (Ace-to-Five low showdown).
/// </summary>
public sealed class RazzFlowHandler : SevenCardStudFlowHandler
{
    public override string GameTypeCode => "RAZZ";

    public override GameRules GetGameRules() => RazzRules.CreateGameRules();
}
