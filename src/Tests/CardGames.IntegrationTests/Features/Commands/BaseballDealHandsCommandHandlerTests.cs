using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.StartHand;

namespace CardGames.IntegrationTests.Features.Commands;

public class BaseballDealHandsCommandHandlerTests : IntegrationTestBase
{
	[Fact]
	public async Task DealHands_ThirdStreet_UsesLowestUpCardForFirstActor()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BASEBALL", 2, ante: 10);

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
		success.CurrentPlayerIndex.Should().Be(rob.SeatPosition);
		success.CurrentPlayerName.Should().Be("Rob");

		var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
		game.BringInPlayerIndex.Should().Be(lynne.SeatPosition);
	}

	[Fact]
	public async Task DealHands_FourthStreet_UsesBestVisibleHandForFirstActor()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BASEBALL", 2, ante: 10);

		var rob = setup.GamePlayers.Single(p => p.SeatPosition == 0);
		var lynne = setup.GamePlayers.Single(p => p.SeatPosition == 1);
		rob.Player.Name = "Rob";
		lynne.Player.Name = "Lynne";

		setup.Game.DealerPosition = lynne.SeatPosition;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
		await ScriptThirdStreetDoorCardsAsync(setup.Game.Id, setup.Game.CurrentHandNumber);
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

	[Fact]
	public async Task DealHands_FourthStreet_UsesVisibleWildCardsForFirstActor()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BASEBALL", 2, ante: 10);

		var rob = setup.GamePlayers.Single(p => p.SeatPosition == 0);
		var lynne = setup.GamePlayers.Single(p => p.SeatPosition == 1);
		rob.Player.Name = "Rob";
		lynne.Player.Name = "Lynne";

		setup.Game.DealerPosition = lynne.SeatPosition;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
		await ScriptThirdStreetDoorCardsAsync(setup.Game.Id, setup.Game.CurrentHandNumber);
		await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		var thirdStreetBoardCards = await DbContext.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
			             && gc.HandNumber == setup.Game.CurrentHandNumber
			             && gc.Location == CardLocation.Board
			             && gc.DealtAtPhase == nameof(Phases.ThirdStreet))
			.ToListAsync();

		thirdStreetBoardCards.Single(c => c.GamePlayerId == rob.Id).Symbol = CardSymbol.Ace;
		thirdStreetBoardCards.Single(c => c.GamePlayerId == rob.Id).Suit = CardSuit.Spades;
		thirdStreetBoardCards.Single(c => c.GamePlayerId == lynne.Id).Symbol = CardSymbol.Deuce;
		thirdStreetBoardCards.Single(c => c.GamePlayerId == lynne.Id).Suit = CardSuit.Hearts;

		var deckCards = await DbContext.GameCards
			.Where(gc => gc.GameId == setup.Game.Id
			             && gc.HandNumber == setup.Game.CurrentHandNumber
			             && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.Take(2)
			.ToListAsync();

		deckCards[0].Symbol = CardSymbol.Queen;
		deckCards[0].Suit = CardSuit.Hearts;
		deckCards[1].Symbol = CardSymbol.Nine;
		deckCards[1].Suit = CardSuit.Diamonds;

		setup.Game.CurrentPhase = nameof(Phases.FourthStreet);
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.CurrentPlayerIndex.Should().Be(lynne.SeatPosition);
		success.CurrentPlayerName.Should().Be("Lynne");
	}

	[Fact]
	public async Task DealHands_ThirdStreet_DealsClockwiseFromTheSeatLeftOfTheDealer()
	{
		var setup = await StartThreeSeatHandAsync();
		await ScriptDeckPrefixAsync(setup.Game.Id, setup.Game.CurrentHandNumber, 9);

		var result = await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var game = await DbContext.Games.SingleAsync(item => item.Id == setup.Game.Id);
		game.BringInPlayerIndex.Should().Be(0);

		await AssertSeatCardsAsync(setup.Game.Id, seatPosition: 1,
			(CardSymbol.Ace, CardSuit.Spades, CardLocation.Hole, false, 1),
			(CardSymbol.King, CardSuit.Hearts, CardLocation.Hole, false, 2),
			(CardSymbol.Queen, CardSuit.Spades, CardLocation.Board, true, 3));
		await AssertSeatCardsAsync(setup.Game.Id, seatPosition: 2,
			(CardSymbol.Jack, CardSuit.Diamonds, CardLocation.Hole, false, 1),
			(CardSymbol.Ten, CardSuit.Clubs, CardLocation.Hole, false, 2),
			(CardSymbol.Nine, CardSuit.Hearts, CardLocation.Board, true, 3));
		await AssertSeatCardsAsync(setup.Game.Id, seatPosition: 0,
			(CardSymbol.Eight, CardSuit.Diamonds, CardLocation.Hole, false, 1),
			(CardSymbol.Seven, CardSuit.Clubs, CardLocation.Hole, false, 2),
			(CardSymbol.Six, CardSuit.Spades, CardLocation.Board, true, 3));
	}

	[Fact]
	public async Task DealHands_FourthStreet_ContinuesClockwiseFromTheSeatLeftOfTheDealer()
	{
		var setup = await StartThreeSeatHandAsync();
		await ScriptDeckPrefixAsync(setup.Game.Id, setup.Game.CurrentHandNumber, ClockwisePrefix.Length);

		var thirdStreet = await Mediator.Send(new DealHandsCommand(setup.Game.Id));
		thirdStreet.IsT0.Should().BeTrue();

		var seat2 = setup.GamePlayers.Single(player => player.SeatPosition == 2);
		var game = await DbContext.Games.SingleAsync(item => item.Id == setup.Game.Id);
		game.CurrentPhase = nameof(Phases.FourthStreet);
		await DbContext.SaveChangesAsync();

		var paused = await Mediator.Send(new DealHandsCommand(setup.Game.Id));
		paused.IsT0.Should().BeTrue();
		paused.AsT0.CurrentPhase.Should().Be(nameof(Phases.BuyCardOffer));

		var declined = await Mediator.Send(new ProcessBuyCardCommand(setup.Game.Id, seat2.PlayerId, false));
		declined.IsT0.Should().BeTrue();

		await AssertSeatCardsAsync(setup.Game.Id, seatPosition: 1,
			(CardSymbol.Five, CardSuit.Hearts, CardLocation.Board, true, 4));
		await AssertSeatCardsAsync(setup.Game.Id, seatPosition: 2,
			(CardSymbol.Four, CardSuit.Diamonds, CardLocation.Board, true, 4));
		await AssertSeatCardsAsync(setup.Game.Id, seatPosition: 0,
			(CardSymbol.Three, CardSuit.Clubs, CardLocation.Board, true, 4));
	}

	[Fact]
	public async Task DealHands_ThirdStreet_FourResumesAtTheNextClockwiseSeat()
	{
		var setup = await StartThreeSeatHandAsync();
		var seat1 = setup.GamePlayers.Single(player => player.SeatPosition == 1);
		(CardSymbol Symbol, CardSuit Suit)[] prefix =
		[
			(CardSymbol.Deuce, CardSuit.Spades),
			(CardSymbol.Three, CardSuit.Hearts),
			(CardSymbol.Four, CardSuit.Hearts),
			(CardSymbol.Five, CardSuit.Diamonds),
			(CardSymbol.Six, CardSuit.Clubs),
			(CardSymbol.Seven, CardSuit.Spades),
			(CardSymbol.Eight, CardSuit.Hearts),
			(CardSymbol.Nine, CardSuit.Diamonds),
			(CardSymbol.Ten, CardSuit.Clubs)
		];
		await ScriptDeckAsync(setup.Game.Id, setup.Game.CurrentHandNumber, prefix);

		var paused = await Mediator.Send(new DealHandsCommand(setup.Game.Id));
		paused.IsT0.Should().BeTrue();
		paused.AsT0.CurrentPhase.Should().Be(nameof(Phases.BuyCardOffer));

		var cardsAtPause = await PlayerCardsAsync(setup.Game.Id);
		cardsAtPause.Should().ContainSingle(card => card.GamePlayer!.SeatPosition == 1 && card.Symbol == CardSymbol.Four);
		cardsAtPause.Should().NotContain(card => card.GamePlayer!.SeatPosition != 1);

		var declined = await Mediator.Send(new ProcessBuyCardCommand(setup.Game.Id, seat1.PlayerId, false));
		declined.IsT0.Should().BeTrue();

		await AssertSeatCardsAsync(setup.Game.Id, seatPosition: 2,
			(CardSymbol.Five, CardSuit.Diamonds, CardLocation.Hole, false, 1),
			(CardSymbol.Six, CardSuit.Clubs, CardLocation.Hole, false, 2),
			(CardSymbol.Seven, CardSuit.Spades, CardLocation.Board, true, 3));
		await AssertSeatCardsAsync(setup.Game.Id, seatPosition: 0,
			(CardSymbol.Eight, CardSuit.Hearts, CardLocation.Hole, false, 1),
			(CardSymbol.Nine, CardSuit.Diamonds, CardLocation.Hole, false, 2),
			(CardSymbol.Ten, CardSuit.Clubs, CardLocation.Board, true, 3));

		var seat0Cards = await PlayerCardsAsync(setup.Game.Id, seatPosition: 0);
		seat0Cards.Should().NotContain(card => card.Symbol == CardSymbol.Five && card.Suit == CardSuit.Diamonds);
	}

	private async Task ScriptThirdStreetDoorCardsAsync(Guid gameId, int handNumber)
	{
		var openingDeck = await DbContext.GameCards
			.Where(gc => gc.GameId == gameId
			             && gc.HandNumber == handNumber
			             && gc.Location == CardLocation.Deck)
			.OrderBy(gc => gc.DealOrder)
			.Take(6)
			.ToListAsync();

		openingDeck[2].Symbol = CardSymbol.King;
		openingDeck[2].Suit = CardSuit.Hearts;
		openingDeck[5].Symbol = CardSymbol.Jack;
		openingDeck[5].Suit = CardSuit.Clubs;
		await DbContext.SaveChangesAsync();
	}

	private static readonly (CardSymbol Symbol, CardSuit Suit)[] ClockwisePrefix =
	[
		(CardSymbol.Ace, CardSuit.Spades),
		(CardSymbol.King, CardSuit.Hearts),
		(CardSymbol.Queen, CardSuit.Spades),
		(CardSymbol.Jack, CardSuit.Diamonds),
		(CardSymbol.Ten, CardSuit.Clubs),
		(CardSymbol.Nine, CardSuit.Hearts),
		(CardSymbol.Eight, CardSuit.Diamonds),
		(CardSymbol.Seven, CardSuit.Clubs),
		(CardSymbol.Six, CardSuit.Spades),
		(CardSymbol.Five, CardSuit.Hearts),
		(CardSymbol.Four, CardSuit.Diamonds),
		(CardSymbol.Three, CardSuit.Clubs)
	];

	private async Task<GameSetup> StartThreeSeatHandAsync()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BASEBALL", 3, ante: 10);
		setup.Game.DealerPosition = 0;
		await DbContext.SaveChangesAsync();
		(await Mediator.Send(new StartHandCommand(setup.Game.Id))).IsT0.Should().BeTrue();
		(await Mediator.Send(new CollectAntesCommand(setup.Game.Id))).IsT0.Should().BeTrue();
		return setup;
	}

	private Task ScriptDeckPrefixAsync(Guid gameId, int handNumber, int count) =>
		ScriptDeckAsync(gameId, handNumber, ClockwisePrefix.Take(count).ToArray());

	private async Task ScriptDeckAsync(
		Guid gameId,
		int handNumber,
		IReadOnlyList<(CardSymbol Symbol, CardSuit Suit)> cards)
	{
		var deck = await DbContext.GameCards
			.Where(card => card.GameId == gameId
			               && card.HandNumber == handNumber
			               && card.Location == CardLocation.Deck)
			.OrderBy(card => card.DealOrder)
			.Take(cards.Count)
			.ToListAsync();

		deck.Should().HaveCount(cards.Count);
		for (var index = 0; index < cards.Count; index++)
		{
			deck[index].Symbol = cards[index].Symbol;
			deck[index].Suit = cards[index].Suit;
		}

		await DbContext.SaveChangesAsync();
	}

	private async Task<List<GameCard>> PlayerCardsAsync(Guid gameId, int? seatPosition = null)
	{
		DbContext.ChangeTracker.Clear();
		var query = DbContext.GameCards
			.Include(card => card.GamePlayer)
			.Where(card => card.GameId == gameId
			               && card.GamePlayerId != null
			               && card.Location != CardLocation.Deck
			               && !card.IsDiscarded);

		if (seatPosition is not null)
			query = query.Where(card => card.GamePlayer!.SeatPosition == seatPosition);

		return await query.ToListAsync();
	}

	private async Task AssertSeatCardsAsync(
		Guid gameId,
		int seatPosition,
		params (CardSymbol Symbol, CardSuit Suit, CardLocation Location, bool Visible, int DealOrder)[] expected)
	{
		var cards = await PlayerCardsAsync(gameId, seatPosition);
		var matched = cards
			.Where(card => expected.Any(item => item.DealOrder == card.DealOrder))
			.OrderBy(card => card.DealOrder)
			.Select(card => (card.Symbol, card.Suit, card.Location, card.IsVisible, card.DealOrder))
			.ToArray();

		matched.Should().Equal(expected);
	}
}