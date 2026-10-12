using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;

namespace CardGames.Poker.Api.Features.Games.BobBarker;

internal static class BobBarkerDealerCard
{
	public static bool IsDealerCard(GameCard card)
	{
		return card.Location == CardLocation.Community
		       && card.GamePlayerId == null
		       && !card.IsDiscarded
		       && !card.IsVisible
		       && string.Equals(card.DealtAtPhase, nameof(Phases.Dealing), StringComparison.OrdinalIgnoreCase);
	}
}
