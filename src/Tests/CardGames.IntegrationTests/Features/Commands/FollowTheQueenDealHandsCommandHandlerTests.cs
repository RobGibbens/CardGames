using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.StartHand;
using CardGames.Poker.Betting;

namespace CardGames.IntegrationTests.Features.Commands;

public class FollowTheQueenDealHandsCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task DealHands_ThirdStreet_UsesLowestUpCardForFirstActor()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FOLLOWTHEQUEEN", 2, ante: 10);

		var rob = setup.GamePlayers.Single(p => p.SeatPosition == 0);
		var lynne = setup.GamePlayers.Single(p => p.SeatPosition == 1);
		rob.Player.Name = "Rob";
		lynne.Player.Name = "Lynne";

		setup.Game.DealerPosition = lynne.SeatPosition;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

		var firstSixDeckCards = await DbContext.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
						 && gc.HandNumber == 1
						 && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.Take(6)
			.ToListAsync();

		firstSixDeckCards[2].Symbol = CardSymbol.King;
		firstSixDeckCards[2].Suit = CardSuit.Hearts;
		firstSixDeckCards[5].Symbol = CardSymbol.Jack;
		firstSixDeckCards[5].Suit = CardSuit.Clubs;

		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.CurrentPlayerIndex.Should().Be(lynne.SeatPosition);
		success.CurrentPlayerName.Should().Be("Lynne");

		var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
		game.BringInPlayerIndex.Should().Be(lynne.SeatPosition);
	}

	[Fact]
	public async Task DealHands_FourthStreet_UsesBestVisibleHandForFirstActor()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FOLLOWTHEQUEEN", 2, ante: 10);

		var rob = setup.GamePlayers.Single(p => p.SeatPosition == 0);
		var lynne = setup.GamePlayers.Single(p => p.SeatPosition == 1);
		rob.Player.Name = "Rob";
		lynne.Player.Name = "Lynne";

		setup.Game.DealerPosition = lynne.SeatPosition;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
		await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		var thirdStreetBoardCards = await DbContext.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
						 && gc.HandNumber == setup.Game.CurrentHandNumber
						 && gc.Location == CardLocation.Board
						 && gc.DealtAtPhase == nameof(Phases.ThirdStreet))
			.ToListAsync();

		thirdStreetBoardCards.Single(c => c.GamePlayerId == rob.Id).Symbol = CardSymbol.King;
		thirdStreetBoardCards.Single(c => c.GamePlayerId == rob.Id).Suit = CardSuit.Hearts;
		thirdStreetBoardCards.Single(c => c.GamePlayerId == lynne.Id).Symbol = CardSymbol.Jack;
		thirdStreetBoardCards.Single(c => c.GamePlayerId == lynne.Id).Suit = CardSuit.Clubs;

		var deckCards = await DbContext.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
						 && gc.HandNumber == setup.Game.CurrentHandNumber
						 && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.Take(2)
			.ToListAsync();

		deckCards[0].Symbol = CardSymbol.Eight;
		deckCards[0].Suit = CardSuit.Spades;
		deckCards[1].Symbol = CardSymbol.Jack;
		deckCards[1].Suit = CardSuit.Hearts;

		setup.Game.CurrentPhase = nameof(Phases.FourthStreet);
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.CurrentPlayerIndex.Should().Be(lynne.SeatPosition);
		success.CurrentPlayerName.Should().Be("Lynne");

		var bettingRound = await DbContext.BettingRounds
			.Where(br => br.GameId == setup.Game.Id && br.Street == nameof(Phases.FourthStreet))
			.OrderByDescending(br => br.StartedAt)
			.FirstAsync();

		bettingRound.CurrentActorIndex.Should().Be(lynne.SeatPosition);
	}
}