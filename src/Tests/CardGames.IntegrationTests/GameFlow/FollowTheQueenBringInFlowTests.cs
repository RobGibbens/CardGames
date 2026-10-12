namespace CardGames.IntegrationTests.GameFlow;

/// <summary>
///     Continuous-play third street posts the bring-in from the lowest door card.
///     An ace is the highest rank. Suit ties rank clubs, diamonds, hearts, then spades.
/// </summary>
public class FollowTheQueenBringInFlowTests : IntegrationTestBase
{
	public static TheoryData<CardSuit, CardSymbol, CardSuit, CardSymbol, int> DoorCards { get; } = new()
	{
		{ CardSuit.Hearts, CardSymbol.Ace, CardSuit.Clubs, CardSymbol.Deuce, 1 },
		{ CardSuit.Hearts, CardSymbol.King, CardSuit.Hearts, CardSymbol.Ace, 0 },
		{ CardSuit.Hearts, CardSymbol.Ace, CardSuit.Clubs, CardSymbol.Ace, 1 }
	};

	[Theory]
	[MemberData(nameof(DoorCards))]
	public async Task DealCardsAsync_PostsBringInFromLowestDoorCard(
		CardSuit aliceDoorSuit,
		CardSymbol aliceDoorSymbol,
		CardSuit bobDoorSuit,
		CardSymbol bobDoorSymbol,
		int bringInSeat)
	{
		var handler = new FollowTheQueenFlowHandler();
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FOLLOWTHEQUEEN", 2);
		var alice = setup.Game.GamePlayers.Single(player => player.SeatPosition == 0);
		var bob = setup.Game.GamePlayers.Single(player => player.SeatPosition == 1);

		setup.Game.CurrentHandNumber = 1;
		setup.Game.DealerPosition = bob.SeatPosition;
		setup.Game.BringIn = 5;
		alice.ChipStack = 1000;
		alice.CurrentBet = 0;
		bob.ChipStack = 1000;
		bob.CurrentBet = 0;
		await DbContext.SaveChangesAsync();

		await SeedPersistedDeckAsync(
			setup.Game.Id,
			setup.Game.CurrentHandNumber,
			(CardSuit.Diamonds, CardSymbol.King),
			(CardSuit.Clubs, CardSymbol.Queen),
			(aliceDoorSuit, aliceDoorSymbol),
			(CardSuit.Spades, CardSymbol.Seven),
			(CardSuit.Diamonds, CardSymbol.Nine),
			(bobDoorSuit, bobDoorSymbol));

		await handler.DealCardsAsync(
			DbContext,
			setup.Game,
			setup.Game.GamePlayers.ToList(),
			DateTimeOffset.UtcNow,
			CancellationToken.None);
		await DbContext.SaveChangesAsync();

		var players = await DbContext.GamePlayers
			.Where(player => player.GameId == setup.Game.Id)
			.ToListAsync();
		var poster = players.Single(player => player.SeatPosition == bringInSeat);
		var other = players.Single(player => player.SeatPosition != bringInSeat);
		var game = await DbContext.Games.SingleAsync(candidate => candidate.Id == setup.Game.Id);

		players.Count(player => player.CurrentBet == 5).Should().Be(1);
		poster.CurrentBet.Should().Be(5);
		poster.ChipStack.Should().Be(995);
		other.CurrentBet.Should().Be(0);
		other.ChipStack.Should().Be(1000);
		game.BringInPlayerIndex.Should().Be(bringInSeat);
	}

	private async Task SeedPersistedDeckAsync(
		Guid gameId,
		int handNumber,
		params (CardSuit Suit, CardSymbol Symbol)[] cards)
	{
		var dealOrder = 0;
		foreach (var (suit, symbol) in cards)
			DbContext.GameCards.Add(new GameCard
			{
				GameId = gameId,
				GamePlayerId = null,
				HandNumber = handNumber,
				Suit = suit,
				Symbol = symbol,
				Location = CardLocation.Deck,
				DealOrder = dealOrder++,
				IsVisible = false,
				IsDiscarded = false,
				DealtAt = DateTimeOffset.UtcNow
			});

		await DbContext.SaveChangesAsync();
	}
}
