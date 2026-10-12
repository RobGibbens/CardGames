using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Hands.StudHands;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.Features.Games.Common;

/// <summary>
///     Loads stud face-up cards in the order they were dealt: each street finished
///     before the next, seats walked clockwise from the seat left of the dealer.
/// </summary>
internal static class StudFaceUpSequence
{
	public static async Task<List<Card>> LoadAsync(
		CardsDbContext context,
		Guid gameId,
		int handNumber,
		int dealerPosition,
		CancellationToken cancellationToken)
	{
		var rawFaceUpCards = await context.GameCards
			.Where(c => c.GameId == gameId &&
			            c.HandNumber == handNumber &&
			            c.IsVisible &&
			            !c.IsDiscarded)
			.Include(c => c.GamePlayer)
			.Select(c => new
			{
				c.Symbol,
				c.Suit,
				c.DealtAtPhase,
				c.DealOrder,
				c.Location,
				SeatPosition = c.GamePlayer != null ? c.GamePlayer.SeatPosition : -1
			})
			.ToListAsync(cancellationToken);

		var seats = rawFaceUpCards
			.GroupBy(card => card.SeatPosition)
			.Select(group => new
			{
				SeatPosition = group.Key,
				Cards = StudOrderHelper.OrderPlayerCards(
					group,
					card => card.DealtAtPhase,
					card => card.Location == CardLocation.Hole,
					card => card.DealOrder)
			})
			.ToList();

		return StudOrderHelper.OrderFaceUpCardsInGlobalDealOrder(
				seats,
				dealerPosition,
				seat => seat.SeatPosition,
				seat => seat.Cards,
				_ => true)
			.Select(card => new Card((Suit)card.Suit, (Symbol)card.Symbol))
			.ToList();
	}
}
