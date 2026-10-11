using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Services;
using StudShowdown = CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.PerformShowdown;

namespace CardGames.IntegrationTests.EndToEnd;

public class RazzGameFlowTests : IntegrationTestBase
{
	[Fact]
	public async Task StartHand_TransitionsToCollectingAntes()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "RAZZ", 4);

		var result = await Mediator.Send(new StartHandCommand(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
	}

	[Fact]
	public async Task FlowHandler_Sequence_MatchesStudStreets()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "RAZZ", 4);
		var handler = FlowHandlerFactory.GetHandler("RAZZ");

		handler.GetNextPhase(setup.Game, nameof(Phases.CollectingAntes)).Should().Be(nameof(Phases.ThirdStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.ThirdStreet)).Should().Be(nameof(Phases.FourthStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet)).Should().Be(nameof(Phases.FifthStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.FifthStreet)).Should().Be(nameof(Phases.SixthStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.SixthStreet)).Should().Be(nameof(Phases.SeventhStreet));
		handler.GetNextPhase(setup.Game, nameof(Phases.SeventhStreet)).Should().Be(nameof(Phases.Showdown));
		handler.GetNextPhase(setup.Game, nameof(Phases.Showdown)).Should().Be(nameof(Phases.Complete));
	}

	[Fact]
	public async Task PerformShowdown_AwardsBestRazzLow_NotHighStudHand()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "RAZZ", 2);
		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;
		await DbContext.SaveChangesAsync();
		await DatabaseSeeder.CreatePotAsync(DbContext, game, 80);

		var playerOne = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
		var playerTwo = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);
		var dealtAt = DateTimeOffset.UtcNow;
		AddStudHand(game.Id, playerOne.Id, "Ks Js 6s 5h Qs 2h 6d", dealtAt);
		AddStudHand(game.Id, playerTwo.Id, "Qc Kh 9d 7h 9h 7d 5s", dealtAt);
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new StudShowdown.PerformShowdownCommand(game.Id));

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Payouts.Should().ContainKey("Player 1");
		success.Payouts.Should().NotContainKey("Player 2");
		success.Payouts["Player 1"].Should().Be(80);
		var winner = success.PlayerHands.Single(hand => hand.IsWinner);
		winner.PlayerName.Should().Be("Player 1");
		winner.HandType.Should().Contain(" low");
		winner.HandType.Should().NotContain("Pair");
		winner.HandType.Should().NotContain("TwoPair");

		var history = ServiceProvider.GetRequiredService<IHandHistoryRecorder>()
			.Should().BeOfType<FakeHandHistoryRecorder>().Subject;
		var recorded = history.RecordedHands.Should().ContainSingle(hand => hand.GameId == game.Id).Subject;
		var showdownCards = recorded.PlayerResults.Single(player => player.PlayerName == "Player 1").ShowdownCards;
		showdownCards.Should().NotBeNull();
		showdownCards.Should().HaveCount(5);
		showdownCards.Should().NotContain("Ks");
	}

	private void AddStudHand(Guid gameId, Guid gamePlayerId, string cards, DateTimeOffset dealtAt)
	{
		var tokens = cards.Split(' ');
		for (var i = 0; i < tokens.Length; i++)
		{
			var isUpCard = i is >= 2 and <= 5;
			DbContext.GameCards.Add(new GameCard
			{
				GameId = gameId,
				GamePlayerId = gamePlayerId,
				HandNumber = 1,
				DealOrder = i + 1,
				Location = isUpCard ? CardLocation.Board : CardLocation.Hole,
				IsVisible = isUpCard,
				Suit = ParseSuit(tokens[i][^1]),
				Symbol = ParseSymbol(tokens[i][..^1]),
				DealtAt = dealtAt,
				DealtAtPhase = i == 6 ? nameof(Phases.SeventhStreet) : nameof(Phases.ThirdStreet)
			});
		}
	}

	private static CardSuit ParseSuit(char suit)
	{
		return suit switch
		{
			'h' => CardSuit.Hearts,
			'd' => CardSuit.Diamonds,
			's' => CardSuit.Spades,
			'c' => CardSuit.Clubs,
			_ => throw new ArgumentOutOfRangeException(nameof(suit), suit, "Unknown suit")
		};
	}

	private static CardSymbol ParseSymbol(string symbol)
	{
		return symbol switch
		{
			"2" => CardSymbol.Deuce,
			"5" => CardSymbol.Five,
			"6" => CardSymbol.Six,
			"7" => CardSymbol.Seven,
			"9" => CardSymbol.Nine,
			"J" => CardSymbol.Jack,
			"Q" => CardSymbol.Queen,
			"K" => CardSymbol.King,
			_ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "Unknown symbol")
		};
	}
}