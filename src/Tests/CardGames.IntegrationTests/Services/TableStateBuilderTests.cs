using System.Text.Json;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;
using CardGames.Poker.Api.Services;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.IntegrationTests.Services;

/// <summary>
///     Integration tests for <see cref="TableStateBuilder" />.
///     Tests building public and private state snapshots for games.
/// </summary>
public class TableStateBuilderTests : IntegrationTestBase
{
	private ITableStateBuilder TableStateBuilder => Scope.ServiceProvider.GetRequiredService<ITableStateBuilder>();

	[Fact]
	public async Task BuildPublicStateAsync_GameNotFound_ReturnsNull()
	{
		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(Guid.NewGuid());

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public async Task BuildPublicStateAsync_ExistingGame_ReturnsState()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.GameId.Should().Be(setup.Game.Id);
	}

	[Fact]
	public async Task BuildPublicStateAsync_IncludesSeats()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.Seats.Should().HaveCount(4);
	}

	[Fact]
	public async Task BuildPublicStateAsync_IncludesPotTotal()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, ante: 10);
		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.TotalPot.Should().Be(40); // 4 players x 10 ante
	}

	[Fact]
	public async Task BuildPublicStateAsync_HoldTheBaseball_AfterStartHand_ExposesBlindPotState()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDTHEBASEBALL", 4, ante: 0);
		setup.Game.SmallBlind = 5;
		setup.Game.BigBlind = 10;
		setup.Game.DealerPosition = 0;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.GameTypeCode.Should().Be("HOLDTHEBASEBALL");
		result.CurrentPhase.Should().Be(nameof(Phases.PreFlop));
		result.TotalPot.Should().Be(15);
		result.DealerSeatIndex.Should().Be(0);
		result.Seats.Should().HaveCount(4);

		var orderedSeats = result.Seats.OrderBy(s => s.SeatIndex).ToList();
		orderedSeats[1].CurrentBet.Should().Be(5, "small blind seat should reflect posted blind in table state");
		orderedSeats[2].CurrentBet.Should().Be(10, "big blind seat should reflect posted blind in table state");
	}

	[Fact]
	public async Task BuildPublicStateAsync_AfterDeal_CardsHidden()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
		await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		// In public state, cards should remain face down without rank/suit payloads.
		result!.Seats.Should().AllSatisfy(seat =>
		{
			seat.Cards.Should().NotBeNull();
			seat.Cards.Should().AllSatisfy(card =>
			{
				card.IsFaceUp.Should().BeFalse();
				card.Rank.Should().BeNull();
				card.Suit.Should().BeNull();
			});
		});
	}

	[Fact]
	public async Task BuildPublicStateAsync_IncludesCurrentPhase()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
		await Mediator.Send(new StartHandCommand(setup.Game.Id));

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
	}

	[Fact]
	public async Task BuildPrivateStateAsync_GameNotFound_ReturnsNull()
	{
		// Act
		var result = await TableStateBuilder.BuildPrivateStateAsync(Guid.NewGuid(), "someuser");

		// Assert
		result.Should().BeNull();
	}

	[Fact]
	public async Task BuildPrivateStateAsync_ExistingGame_ReturnsState()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
		var playerEmail = setup.Players[0].Email;

		// Act
		var result = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, playerEmail!);

		// Assert
		result.Should().NotBeNull();
	}

	[Fact]
	public async Task BuildPrivateStateAsync_AfterDeal_ShowsPlayerCards()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
		var playerEmail = setup.Players[0].Email;

		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
		await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		// Act
		var result = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, playerEmail!);

		// Assert
		result.Should().NotBeNull();
		result!.Hand.Should().NotBeEmpty();
		result.Hand.Should().HaveCount(5);
	}

	[Fact]
	public async Task BuildPrivateStateAsync_BobBarker_DrawPhase_AllEligiblePlayersCanChooseShowcaseConcurrently()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BOBBARKER", 3, ante: 0);
		var game = setup.Game;
		var now = DateTimeOffset.UtcNow;

		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = "BOBBARKER";
		game.CurrentPhase = nameof(Phases.DrawPhase);
		game.Status = GameStatus.InProgress;
		game.CurrentDrawPlayerIndex = setup.GamePlayers[0].SeatPosition;
		game.CurrentPlayerIndex = setup.GamePlayers[0].SeatPosition;

		foreach (var gamePlayer in setup.GamePlayers)
		{
			gamePlayer.HasDrawnThisRound = false;

			for (var cardIndex = 0; cardIndex < 5; cardIndex++)
				DbContext.GameCards.Add(new GameCard
				{
					Id = Guid.CreateVersion7(),
					GameId = game.Id,
					GamePlayerId = gamePlayer.Id,
					HandNumber = 1,
					Location = CardLocation.Hand,
					Suit = (CardSuit)(cardIndex % 4),
					Symbol = (CardSymbol)(cardIndex + 1),
					DealOrder = cardIndex + 1,
					IsVisible = false,
					IsDiscarded = false,
					DealtAt = now,
					DealtAtPhase = nameof(Phases.Dealing)
				});
		}

		await DbContext.SaveChangesAsync();

		var firstPlayerState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, setup.Players[0].Email!);
		var secondPlayerState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, setup.Players[1].Email!);

		firstPlayerState.Should().NotBeNull();
		secondPlayerState.Should().NotBeNull();
		firstPlayerState!.Draw.Should().NotBeNull();
		secondPlayerState!.Draw.Should().NotBeNull();
		firstPlayerState.Draw!.IsMyTurnToDraw.Should().BeTrue();
		secondPlayerState.Draw!.IsMyTurnToDraw.Should().BeTrue();
	}

	[Fact]
	public async Task BuildPrivateStateAsync_BobBarker_UsesExactlyTwoHoleCardsForHandDescription()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BOBBARKER", 2, ante: 0);
		var game = setup.Game;
		var hero = setup.GamePlayers[0];
		var heroEmail = setup.Players[0].Email!;
		var now = DateTimeOffset.UtcNow;

		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = "BOBBARKER";
		game.CurrentPhase = nameof(Phases.River);
		game.Status = GameStatus.InProgress;

		hero.VariantState = "{\"SelectedShowcaseDealOrder\":5}";

		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Queen,
				DealOrder = 1,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing),
				IsVisible = false
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Queen,
				DealOrder = 2,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing),
				IsVisible = false
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Diamonds,
				Symbol = CardSymbol.King,
				DealOrder = 3,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing),
				IsVisible = false
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.King,
				DealOrder = 4,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing),
				IsVisible = false
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Jack,
				DealOrder = 5,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing),
				IsVisible = false
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Six,
				DealOrder = 6,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Ten,
				DealOrder = 7,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Four,
				DealOrder = 8,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Diamonds,
				Symbol = CardSymbol.Deuce,
				DealOrder = 9,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Turn),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Six,
				DealOrder = 10,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.River),
				IsVisible = true
			});

		await DbContext.SaveChangesAsync();

		var state = await TableStateBuilder.BuildPrivateStateAsync(game.Id, heroEmail);

		state.Should().NotBeNull();
		state!.HandEvaluationDescription.Should().Be("Two pair, Kings and Sixes");
	}

	[Fact]
	public async Task BuildPrivateStateAsync_HoldTheBaseball_PreflopAndFlop_ApplyWildCardEvaluation()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDTHEBASEBALL", 2);
		var game = setup.Game;
		var hero = setup.GamePlayers[0];
		var heroEmail = setup.Players[0].Email!;

		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.PreFlop);
		game.Status = GameStatus.InProgress;

		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Nine,
				DealOrder = 1,
				DealtAtPhase = nameof(Phases.PreFlop),
				IsVisible = false
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.Queen,
				DealOrder = 2,
				DealtAtPhase = nameof(Phases.PreFlop),
				IsVisible = false
			});

		await DbContext.SaveChangesAsync();

		// Act: preflop with 9h (wild) + Qs
		var preflopState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, heroEmail);

		// Assert
		preflopState.Should().NotBeNull();
		preflopState!.HandEvaluationDescription.Should().Be("Pair of Queens");

		// Arrange flop: 5c, 7h, Qh
		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Five,
				DealOrder = 3,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Seven,
				DealOrder = 4,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Queen,
				DealOrder = 5,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			});

		game.CurrentPhase = nameof(Phases.Flop);
		await DbContext.SaveChangesAsync();

		// Act: after flop should still use wild-card evaluation
		var flopState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, heroEmail);

		// Assert
		flopState.Should().NotBeNull();
		flopState!.HandEvaluationDescription.Should().Be("Three of a kind, Queens");
	}

	[Fact]
	public async Task BuildPrivateStateAsync_Klondike_HandDescriptionUpdatesWhenKlondikeCardRevealed()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KLONDIKE", 2);
		var game = setup.Game;
		var hero = setup.GamePlayers[0];
		var heroEmail = setup.Players[0].Email!;

		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.PreFlop);
		game.Status = GameStatus.InProgress;

		// Hero's hole cards: 7c, As
		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Seven,
				DealOrder = 1,
				DealtAtPhase = nameof(Phases.PreFlop),
				IsVisible = false
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.Ace,
				DealOrder = 2,
				DealtAtPhase = nameof(Phases.PreFlop),
				IsVisible = false
			});

		// Klondike Card (face-down initially): 7 of Diamonds
		DbContext.GameCards.Add(new GameCard
		{
			GameId = game.Id,
			HandNumber = 1,
			Location = CardLocation.Community,
			Suit = CardSuit.Diamonds,
			Symbol = CardSymbol.Seven,
			DealOrder = 3,
			DealtAtPhase = "KlondikeCard",
			IsVisible = false
		});

		// Flop community cards: 3h, 9d, Kd (gaps too wide for straights with 2 wilds)
		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Three,
				DealOrder = 4,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Diamonds,
				Symbol = CardSymbol.Nine,
				DealOrder = 5,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = 1,
				Location = CardLocation.Community,
				Suit = CardSuit.Diamonds,
				Symbol = CardSymbol.King,
				DealOrder = 6,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			});

		await DbContext.SaveChangesAsync();

		// Act: Before Klondike card is revealed, one phantom wild card is used.
		// Hole: 7c, As + Community: 3h, 9d, Kd + 1 phantom wild → best hand = Pair of Aces
		var beforeRevealState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, heroEmail);

		beforeRevealState.Should().NotBeNull();
		beforeRevealState!.HandEvaluationDescription.Should().Be("Pair of Aces");

		// Arrange: Reveal the Klondike Card (simulate KlondikeReveal phase)
		var klondikeCard = await DbContext.GameCards
			.FirstAsync(c => c.GameId == game.Id && c.DealtAtPhase == "KlondikeCard");
		klondikeCard.IsVisible = true;
		game.CurrentPhase = "KlondikeReveal";
		await DbContext.SaveChangesAsync();

		// Act: After reveal, 7d is the Klondike Card → all 7s are wild.
		// Hero holds 7c (wild) and community has 7d (wild): 2 wilds + As → Three of a Kind Aces.
		var afterRevealState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, heroEmail);

		afterRevealState.Should().NotBeNull();
		afterRevealState!.HandEvaluationDescription.Should().Be("Three of a kind, Aces");
	}

	[Fact]
	public async Task BuildPublicAndPrivateState_Baseball_HidesBoughtHoleCard()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BASEBALL", 2, ante: 0);
		var game = setup.Game;
		var seat0 = setup.GamePlayers[0];
		var seat1 = setup.GamePlayers[1];
		var now = DateTimeOffset.UtcNow;

		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = "BASEBALL";
		game.CurrentPhase = nameof(Phases.FourthStreet);
		game.Status = GameStatus.InProgress;

		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = seat0.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.King,
				DealOrder = 1,
				IsVisible = false,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = seat0.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Diamonds,
				Symbol = CardSymbol.Queen,
				DealOrder = 2,
				IsVisible = false,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = seat0.Id,
				HandNumber = 1,
				Location = CardLocation.Board,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Four,
				DealOrder = 3,
				IsVisible = true,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.FourthStreet)
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = seat0.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.Ace,
				DealOrder = 4,
				IsVisible = false,
				IsBuyCard = true,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.FourthStreet)
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = seat1.Id,
				HandNumber = 1,
				Location = CardLocation.Hole,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Jack,
				DealOrder = 1,
				IsVisible = false,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			});

		await DbContext.SaveChangesAsync();

		var publicState = await TableStateBuilder.BuildPublicStateAsync(game.Id);

		publicState.Should().NotBeNull();
		var publicSeat0 = publicState!.Seats.Single(seat => seat.SeatIndex == 0);
		publicSeat0.Cards.Should().Contain(card => card.IsFaceUp && card.Rank == "4");
		publicSeat0.Cards.Should().NotContain(card => card.Rank == "A");
		publicState.Seats.SelectMany(seat => seat.Cards).Should().NotContain(card =>
			card.Rank == "A" || card.Suit == "Spades");

		var publicSeat1 = publicState.Seats.Single(seat => seat.SeatIndex == 1);
		publicSeat1.Cards.Should().NotContain(card => card.IsFaceUp);

		var privateState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, setup.Players[0].Email!);

		privateState.Should().NotBeNull();
		privateState!.Hand.Should().Contain(card =>
			card.Rank == "A" && card.Suit == "Spades" && !card.IsPubliclyVisible);
		privateState.Hand.Should().Contain(card =>
			card.Rank == "4" && card.IsPubliclyVisible);
	}

	[Fact]
	public async Task BuildPublicStateAsync_SevenCardStud_ShowsBoardCards()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
		await Mediator.Send(new StartHandCommand(setup.Game.Id));

		// Re-fetch to get updated state
		var game = await DbContext.Games
			.Include(g => g.GamePlayers)
			.FirstAsync(g => g.Id == setup.Game.Id);

		// Deal cards using flow handler
		var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");
		await handler.DealCardsAsync(DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		// Seven Card Stud should show board (up) cards for all players
		result!.Seats.Should().HaveCount(4);
		result.Seats.Should().OnlyContain(s => s.Cards.Any());

		// Check that at least one card is face up (visible) for each player
		foreach (var seat in result.Seats)
		{
			seat.Cards.Should().Contain(c => c.IsFaceUp && c.Rank != null && c.Suit != null);
			// And ensure we have face-down cards too (Rank/Suit should be null/hidden)
			seat.Cards.Should().Contain(c => !c.IsFaceUp && c.Rank == null && c.Suit == null);
		}
	}

	[Fact]
	public async Task BuildPublicStateAsync_KingsAndLows_InDropOrStayPhase()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
		await Mediator.Send(
			new Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand.StartHandCommand(setup.Game.Id));

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.CurrentPhase.Should().Be(nameof(Phases.DropOrStay));
	}

	[Fact]
	public async Task BuildPublicStateAsync_KingsAndLows_DeckWinAtComplete_PublishesShowdownNotFoldWin()
	{
		var setup = await SeedKingsAndLowsPlayerVsDeckAsync(
			nameof(Phases.Complete),
			potAwarded: true,
			winReason: "Lost to deck's pair of Jacks - pot carries forward",
			includeDeckHand: true);

		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		result.Should().NotBeNull();
		result!.Showdown.Should().NotBeNull();
		result.Showdown!.WonByFold.Should().BeFalse();

		var staying = result.Showdown.PlayerResults.Should()
			.ContainSingle(r => r.PlayerName == setup.Players[0].Name).Which;
		staying.HandDescription.Should().NotBeNull();
		staying.HandRanking.Should().NotBeNull();
		staying.Cards.Should().HaveCount(5);
		staying.Cards.Should().OnlyContain(c => c.IsFaceUp);
		staying.Cards.Should().Contain(c => c.Rank == "7" && c.Suit == "Hearts");

		var deck = result.Showdown.PlayerResults.Should()
			.ContainSingle(r => r.PlayerName == "The Deck").Which;
		deck.IsWinner.Should().BeTrue();
		deck.Cards.Should().HaveCount(5);
		deck.Cards.Should().OnlyContain(c => c.IsFaceUp);
		deck.Cards.Should().Contain(c => c.Rank == "J" && c.Suit == "Hearts");

		var seat0 = result.Seats.Single(s => s.SeatIndex == 0);
		seat0.Cards.Should().HaveCount(5);
		seat0.Cards.Should().OnlyContain(c => c.IsFaceUp);
		seat0.Cards.Should().Contain(c => c.Rank == "7" && c.Suit == "Hearts");
		seat0.Cards.Should().Contain(c => c.Rank == "7" && c.Suit == "Diamonds");

		result.Seats.Where(s => s.SeatIndex is 1 or 2).Should().AllSatisfy(seat =>
			seat.Cards.Should().NotContain(c => c.Rank == "7" || c.Rank == "2" || c.Rank == "3" || c.Rank == "4"));
	}

	[Fact]
	public async Task BuildPublicStateAsync_KingsAndLows_PlayerVsDeckAtShowdownBeforeAward_IsNotWinByFold()
	{
		var setup = await SeedKingsAndLowsPlayerVsDeckAsync(
			nameof(Phases.Showdown),
			potAwarded: false,
			winReason: null,
			includeDeckHand: true);

		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		result.Should().NotBeNull();
		result!.Showdown.Should().NotBeNull();
		result.Showdown!.WonByFold.Should().BeFalse();

		var staying = result.Showdown.PlayerResults.Should()
			.ContainSingle(r => r.PlayerName == setup.Players[0].Name).Which;
		staying.Cards.Should().Contain(c => c.IsFaceUp && c.Rank == "7" && c.Suit == "Hearts");
		result.Showdown.PlayerResults.Should().Contain(r => r.PlayerName == "The Deck");
	}

	[Fact]
	public async Task BuildPublicStateAsync_KingsAndLows_AllOthersDropped_StaysWinByFold()
	{
		var setup = await SeedKingsAndLowsPlayerVsDeckAsync(
			nameof(Phases.Complete),
			potAwarded: true,
			winReason: "All others dropped",
			includeDeckHand: false);

		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		result.Should().NotBeNull();
		result!.Showdown.Should().NotBeNull();
		result.Showdown!.WonByFold.Should().BeTrue();

		var staying = result.Showdown.PlayerResults.Should()
			.ContainSingle(r => r.PlayerName == setup.Players[0].Name).Which;
		staying.HandDescription.Should().BeNull();
		staying.Cards.Where(c => c.Rank == null && c.Suit == null).Should().HaveCount(3);
		result.Showdown.PlayerResults.Should().NotContain(r => r.PlayerName == "The Deck");
	}

	[Fact]
	public async Task BuildPublicStateAsync_ScrewYourNeighbor_VisibleKingIsShownToAllPlayers()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 3);
		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.KeepOrTrade);
		game.Status = GameStatus.InProgress;

		var seat0 = setup.GamePlayers.First(gp => gp.SeatPosition == 0);
		var seat1 = setup.GamePlayers.First(gp => gp.SeatPosition == 1);

		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = seat0.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.King,
				DealOrder = 1,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = seat1.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Five,
				DealOrder = 1,
				IsVisible = false
			});

		await DbContext.SaveChangesAsync();

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

		// Assert
		result.Should().NotBeNull();
		var seat0Public = result!.Seats.First(s => s.SeatIndex == 0);
		var seat1Public = result.Seats.First(s => s.SeatIndex == 1);

		seat0Public.Cards.Should().ContainSingle();
		seat0Public.Cards[0].IsFaceUp.Should().BeTrue();
		seat0Public.Cards[0].Rank.Should().Be("K");
		seat0Public.Cards[0].Suit.Should().NotBeNull();

		seat1Public.Cards.Should().ContainSingle();
		seat1Public.Cards[0].IsFaceUp.Should().BeFalse();
		seat1Public.Cards[0].Rank.Should().BeNull();
		seat1Public.Cards[0].Suit.Should().BeNull();
	}

	[Fact]
	public async Task BuildPublicStateAsync_ScrewYourNeighbor_EndedGameStillIncludesCompleteShowdownForWinner()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext,
			"SCREWYOURNEIGHBOR",
			2,
			25,
			25);

		await Mediator.Send(new StartHandCommand(setup.Game.Id));

		var game = await DbContext.Games
			.Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
			.FirstAsync(g => g.Id == setup.Game.Id);

		var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
		var winner = players[0];
		var loser = players[1];

		var handCards = await DbContext.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Hand &&
			             gc.GamePlayerId != null)
			.ToListAsync();

		handCards.First(c => c.GamePlayerId == winner.Id).Symbol = CardSymbol.Four;
		handCards.First(c => c.GamePlayerId == winner.Id).Suit = CardSuit.Hearts;
		handCards.First(c => c.GamePlayerId == loser.Id).Symbol = CardSymbol.Ace;
		handCards.First(c => c.GamePlayerId == loser.Id).Suit = CardSuit.Spades;
		await DbContext.SaveChangesAsync();

		for (var i = 0; i < 2; i++)
		{
			game = await DbContext.Games
				.Include(g => g.GamePlayers)
				.AsNoTracking()
				.FirstAsync(g => g.Id == setup.Game.Id);

			if (game.CurrentPhase != nameof(Phases.KeepOrTrade)) break;

			var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
			var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
			keepResult.IsT0.Should().BeTrue();
		}

		var showdownResult = await Mediator.Send(new PerformShowdownCommand(setup.Game.Id));
		showdownResult.IsT0.Should().BeTrue();

		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		result.Should().NotBeNull();
		result!.CurrentPhase.Should().Be("Ended");
		result.Showdown.Should().NotBeNull();
		result.Showdown!.IsComplete.Should().BeTrue();
		result.Showdown.PlayerResults.Should().ContainSingle(r => r.PlayerName == winner.Player.Name && r.IsWinner);
		result.Showdown.PlayerResults.Should().ContainSingle(r => r.PlayerName == loser.Player.Name && !r.IsWinner);
	}

	[Fact]
	public async Task BuildPublicStateAsync_ScrewYourNeighbor_ShowdownExcludesZeroChipSitOutWithoutHand()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext,
			"SCREWYOURNEIGHBOR",
			3,
			25,
			25);

		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;

		var players = await DbContext.GamePlayers
			.Include(gp => gp.Player)
			.Where(gp => gp.GameId == game.Id)
			.OrderBy(gp => gp.SeatPosition)
			.ToListAsync();

		var winner = players[0];
		var bustedLoser = players[1];
		var sittingOut = players[2];

		bustedLoser.ChipStack = 0;
		bustedLoser.IsSittingOut = true;
		sittingOut.ChipStack = 0;
		sittingOut.IsSittingOut = true;

		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = winner.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Four,
				DealOrder = 1,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = bustedLoser.Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.Ace,
				DealOrder = 1,
				IsVisible = true
			});

		await DbContext.SaveChangesAsync();

		var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

		result.Should().NotBeNull();
		result!.Showdown.Should().NotBeNull();
		result.Showdown!.PlayerResults.Should().HaveCount(2);
		result.Showdown.PlayerResults.Should().ContainSingle(r => r.PlayerName == winner.Player!.Name && r.IsWinner);
		result.Showdown.PlayerResults.Should()
			.ContainSingle(r => r.PlayerName == bustedLoser.Player!.Name && !r.IsWinner);
		result.Showdown.PlayerResults.Should().NotContain(r => r.PlayerName == sittingOut.Player!.Name);
	}

	[Fact]
	public async Task BuildPublicStateAsync_RebuyGracePause_IncludesBustedSitOutPlayersInShortPlayers()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, 100,
			10);

		var players = await DbContext.GamePlayers
			.Include(gp => gp.Player)
			.Where(gp => gp.GameId == setup.Game.Id)
			.OrderBy(gp => gp.SeatPosition)
			.ToListAsync();

		players[0].ChipStack = 100;
		players[0].Status = GamePlayerStatus.Active;
		players[0].IsSittingOut = false;
		players[1].ChipStack = 0;
		players[1].Status = GamePlayerStatus.SittingOut;
		players[1].IsSittingOut = true;

		setup.Game.IsPausedForChipCheck = true;
		setup.Game.IsPausedForRebuyGrace = true;
		setup.Game.RebuyGraceStartedAt = DateTimeOffset.UtcNow;
		setup.Game.RebuyGraceEndsAt =
			DateTimeOffset.UtcNow.AddSeconds(ContinuousPlayBackgroundService.CashRebuyGraceDurationSeconds);

		await DbContext.SaveChangesAsync();

		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		result.Should().NotBeNull();
		result!.ChipCheckPause.Should().NotBeNull();
		result.ChipCheckPause!.PauseType.Should().Be("RebuyGrace");
		result.ChipCheckPause.ShortPlayers.Should().ContainSingle();
		result.ChipCheckPause.ShortPlayers![0].PlayerName.Should().Be(players[1].Player!.Name);
		result.ChipCheckPause.ShortPlayers[0].CurrentChips.Should().Be(0);
	}

	[Fact]
	public async Task
		BuildPublicStateAsync_RebuyGracePause_KeepsMultipleZeroChipObserversInShortPlayersAfterAnotherPlayerRebuys()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, 100,
			10);

		var players = await DbContext.GamePlayers
			.Include(gp => gp.Player)
			.Where(gp => gp.GameId == setup.Game.Id)
			.OrderBy(gp => gp.SeatPosition)
			.ToListAsync();

		players[0].ChipStack = 100;
		players[0].Status = GamePlayerStatus.Active;
		players[0].IsSittingOut = false;

		players[1].ChipStack = 75;
		players[1].Status = GamePlayerStatus.Active;
		players[1].IsSittingOut = false;

		players[2].ChipStack = 0;
		players[2].Status = GamePlayerStatus.SittingOut;
		players[2].IsSittingOut = true;

		players[3].ChipStack = 0;
		players[3].Status = GamePlayerStatus.SittingOut;
		players[3].IsSittingOut = true;

		setup.Game.IsPausedForChipCheck = true;
		setup.Game.IsPausedForRebuyGrace = true;
		setup.Game.RebuyGraceStartedAt = DateTimeOffset.UtcNow;
		setup.Game.RebuyGraceEndsAt =
			DateTimeOffset.UtcNow.AddSeconds(ContinuousPlayBackgroundService.CashRebuyGraceDurationSeconds);

		await DbContext.SaveChangesAsync();

		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		result.Should().NotBeNull();
		result!.ChipCheckPause.Should().NotBeNull();
		result.ChipCheckPause!.PauseType.Should().Be("RebuyGrace");
		result.ChipCheckPause.ShortPlayers.Should().HaveCount(2);
		result.ChipCheckPause.ShortPlayers!.Select(sp => sp.SeatIndex).Should()
			.BeEquivalentTo([players[2].SeatPosition, players[3].SeatPosition]);
		result.ChipCheckPause.ShortPlayers.Select(sp => sp.CurrentChips).Should().OnlyContain(chips => chips == 0);
	}

	[Fact]
	public async Task BuildPublicStateAsync_EliminatedTournamentPlayer_IsShownAsObserving()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, 100,
			10);

		var players = await DbContext.GamePlayers
			.Include(gp => gp.Player)
			.Where(gp => gp.GameId == setup.Game.Id)
			.OrderBy(gp => gp.SeatPosition)
			.ToListAsync();

		players[1].ChipStack = 0;
		players[1].Status = GamePlayerStatus.Eliminated;
		players[1].IsSittingOut = true;
		players[1].HasFolded = true;
		setup.Game.TournamentBuyIn = 100;

		await DbContext.SaveChangesAsync();

		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		result.Should().NotBeNull();
		result!.Seats.Should().ContainSingle(s =>
			s.SeatIndex == players[1].SeatPosition &&
			s.SittingOutReason == "Observing" &&
			s.IsSittingOut);
	}

	[Fact]
	public async Task BuildPublicStateAsync_FollowTheQueen_DynamicWildRank_UsesNextCardAfterQueenInGlobalDealOrder()
	{
		// Arrange
		// Repro: If DealOrder resets per street for visible board cards, a later street card can end up
		// interleaved ahead of earlier street cards when sorting only by DealOrder.
		// We expect the "follow" wild rank to be the card dealt immediately after the last face-up Queen
		// in true deal sequence (street order + rotation), not "the next card dealt to the Queen's owner".
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FOLLOWTHEQUEEN", 2);

		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = "FourthStreet";
		game.DealerPosition = 1; // Seat 1 is dealer, so dealing starts at seat 0 (left of dealer)
		await DbContext.SaveChangesAsync();

		var rob = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
		var lynne = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);

		// Third street visible cards (DealOrder 1..N within street)
		DbContext.GameCards.Add(new GameCard
		{
			GameId = game.Id,
			GamePlayerId = rob.Id,
			HandNumber = game.CurrentHandNumber,
			Location = CardLocation.Board,
			Suit = CardSuit.Clubs,
			Symbol = CardSymbol.Queen,
			DealtAtPhase = "ThirdStreet",
			DealOrder = 1,
			IsVisible = true
		});
		DbContext.GameCards.Add(new GameCard
		{
			GameId = game.Id,
			GamePlayerId = lynne.Id,
			HandNumber = game.CurrentHandNumber,
			Location = CardLocation.Board,
			Suit = CardSuit.Hearts,
			Symbol = CardSymbol.Four,
			DealtAtPhase = "ThirdStreet",
			DealOrder = 2,
			IsVisible = true
		});

		// Fourth street visible card with DealOrder reset back to 1
		DbContext.GameCards.Add(new GameCard
		{
			GameId = game.Id,
			GamePlayerId = rob.Id,
			HandNumber = game.CurrentHandNumber,
			Location = CardLocation.Board,
			Suit = CardSuit.Hearts,
			Symbol = CardSymbol.Ace,
			DealtAtPhase = "FourthStreet",
			DealOrder = 1,
			IsVisible = true
		});

		await DbContext.SaveChangesAsync();

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.SpecialRules.Should().NotBeNull();
		result.SpecialRules!.WildCardRules.Should().NotBeNull();
		result.SpecialRules.WildCardRules!.WildRanks.Should().NotBeNull();
		result.SpecialRules.WildCardRules.WildRanks!.Should().Contain("Q");
		result.SpecialRules.WildCardRules.WildRanks!.Should().Contain("4");
		result.SpecialRules.WildCardRules.WildRanks!.Should().NotContain("A");
	}

	[Fact]
	public async Task BuildPublicStateAsync_DealersChoiceGame_SetsIsDealersChoice()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateDealersChoiceGameSetupAsync(DbContext, 3);

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.IsDealersChoice.Should().BeTrue();
	}

	[Fact]
	public async Task BuildPublicStateAsync_DealersChoiceGame_SetsDealersChoiceDealerPosition()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateDealersChoiceGameSetupAsync(DbContext, 3, dealerSeatPosition: 1);

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.DealersChoiceDealerPosition.Should().Be(1);
	}

	[Fact]
	public async Task BuildPublicStateAsync_NonDealersChoiceGame_DealersChoiceFieldsNotSet()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 3);

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.IsDealersChoice.Should().BeFalse();
		result.DealersChoiceDealerPosition.Should().BeNull();
	}

	[Fact]
	public async Task BuildPublicStateAsync_SouthDakotaShowdown_BestCardIndexesUseThreeHoleAndTwoCommunity()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SOUTHDAKOTA", 2);
		var game = setup.Game;
		var hero = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).First();
		var villain = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).Last();

		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;

		// Hero hole cards (5): Ah As Kd Qc 2d
		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Ace,
				DealOrder = 1,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.Ace,
				DealOrder = 2,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Diamonds,
				Symbol = CardSymbol.King,
				DealOrder = 3,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Queen,
				DealOrder = 4,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = hero.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Diamonds,
				Symbol = CardSymbol.Deuce,
				DealOrder = 5,
				IsVisible = true
			});

		// Villain hole cards (5): all low cards
		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = villain.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Three,
				DealOrder = 1,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = villain.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Spades,
				Symbol = CardSymbol.Four,
				DealOrder = 2,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = villain.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Five,
				DealOrder = 3,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = villain.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Diamonds,
				Symbol = CardSymbol.Six,
				DealOrder = 4,
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				GamePlayerId = villain.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Hand,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Seven,
				DealOrder = 5,
				IsVisible = true
			});

		// Community cards (3 total for South Dakota): Kh Qh Jc
		DbContext.GameCards.AddRange(
			new GameCard
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Community,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.King,
				DealOrder = 1,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Community,
				Suit = CardSuit.Hearts,
				Symbol = CardSymbol.Queen,
				DealOrder = 2,
				DealtAtPhase = nameof(Phases.Flop),
				IsVisible = true
			},
			new GameCard
			{
				GameId = game.Id,
				HandNumber = game.CurrentHandNumber,
				Location = CardLocation.Community,
				Suit = CardSuit.Clubs,
				Symbol = CardSymbol.Jack,
				DealOrder = 3,
				DealtAtPhase = nameof(Phases.Turn),
				IsVisible = true
			});

		await DbContext.SaveChangesAsync();

		// Act
		var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

		// Assert
		result.Should().NotBeNull();
		result!.Showdown.Should().NotBeNull();

		var heroResult = result.Showdown!.PlayerResults.FirstOrDefault(p => p.PlayerName == hero.Player.Name);
		heroResult.Should().NotBeNull();
		heroResult!.Cards.Should()
			.HaveCount(8, "5 hole cards + 3 community cards should be present at South Dakota showdown");
		heroResult.BestCardIndexes.Should().NotBeNull();
		heroResult.BestCardIndexes!.Should().HaveCount(5);

		var holeIndexes = heroResult.BestCardIndexes!.Where(i => i is >= 0 and <= 4).ToList();
		var communityIndexes = heroResult.BestCardIndexes!.Where(i => i >= 5).ToList();

		holeIndexes.Should().HaveCount(3, "South Dakota must highlight exactly 3 hole cards in the best hand");
		communityIndexes.Should()
			.HaveCount(2, "South Dakota must highlight exactly 2 community cards in the best hand");
	}

	[Fact]
	public async Task BuildPublicStateAsync_CompletedTenthHand_IncludesDeterministicWinningSoundCue()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext,
			"SCREWYOURNEIGHBOR",
			2,
			25,
			25);

		setup.Game.CurrentHandNumber = 9;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));

		var game = await DbContext.Games
			.Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
			.FirstAsync(g => g.Id == setup.Game.Id);

		var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
		var winner = players[0];
		var loser = players[1];

		var handCards = await DbContext.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Hand &&
			             gc.GamePlayerId != null)
			.ToListAsync();

		handCards.First(c => c.GamePlayerId == winner.Id).Symbol = CardSymbol.Four;
		handCards.First(c => c.GamePlayerId == winner.Id).Suit = CardSuit.Hearts;
		handCards.First(c => c.GamePlayerId == loser.Id).Symbol = CardSymbol.Ace;
		handCards.First(c => c.GamePlayerId == loser.Id).Suit = CardSuit.Spades;
		await DbContext.SaveChangesAsync();

		for (var i = 0; i < 2; i++)
		{
			game = await DbContext.Games
				.Include(g => g.GamePlayers)
				.AsNoTracking()
				.FirstAsync(g => g.Id == setup.Game.Id);

			if (game.CurrentPhase != nameof(Phases.KeepOrTrade)) break;

			var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
			var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
			keepResult.IsT0.Should().BeTrue();
		}

		var showdownResult = await Mediator.Send(new PerformShowdownCommand(setup.Game.Id));
		showdownResult.IsT0.Should().BeTrue();

		var firstResult = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);
		var secondResult = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		firstResult.Should().NotBeNull();
		firstResult!.SoundEffects.Should().ContainSingle();
		firstResult.SoundEffects![0].EventKey.Should().Be("winning");
		firstResult.SoundEffects[0].HandNumber.Should().Be(10);
		firstResult.SoundEffects[0].CueKey.Should().StartWith("winning:10:");
		firstResult.SoundEffects[0].Source.Should().Be("/sounds/soundboard/winning/pay_dat_man_his_money.mp3");

		secondResult.Should().NotBeNull();
		secondResult!.SoundEffects.Should().ContainSingle();
		secondResult.SoundEffects![0].Source.Should().Be(firstResult.SoundEffects[0].Source);
		secondResult.SoundEffects[0].CueKey.Should().Be(firstResult.SoundEffects[0].CueKey);
	}

	[Fact]
	public async Task BuildPublicStateAsync_CompletedNonTenthHand_DoesNotIncludeWinningSoundCue()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext,
			"SCREWYOURNEIGHBOR",
			2,
			25,
			25);

		await Mediator.Send(new StartHandCommand(setup.Game.Id));

		var game = await DbContext.Games
			.Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
			.FirstAsync(g => g.Id == setup.Game.Id);

		var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
		var winner = players[0];
		var loser = players[1];

		var handCards = await DbContext.GameCards
			.Where(gc => gc.GameId == game.Id &&
			             gc.HandNumber == game.CurrentHandNumber &&
			             gc.Location == CardLocation.Hand &&
			             gc.GamePlayerId != null)
			.ToListAsync();

		handCards.First(c => c.GamePlayerId == winner.Id).Symbol = CardSymbol.Four;
		handCards.First(c => c.GamePlayerId == winner.Id).Suit = CardSuit.Hearts;
		handCards.First(c => c.GamePlayerId == loser.Id).Symbol = CardSymbol.Ace;
		handCards.First(c => c.GamePlayerId == loser.Id).Suit = CardSuit.Spades;
		await DbContext.SaveChangesAsync();

		for (var i = 0; i < 2; i++)
		{
			game = await DbContext.Games
				.Include(g => g.GamePlayers)
				.AsNoTracking()
				.FirstAsync(g => g.Id == setup.Game.Id);

			if (game.CurrentPhase != nameof(Phases.KeepOrTrade)) break;

			var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
			var keepResult = await Mediator.Send(new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
			keepResult.IsT0.Should().BeTrue();
		}

		var showdownResult = await Mediator.Send(new PerformShowdownCommand(setup.Game.Id));
		showdownResult.IsT0.Should().BeTrue();

		var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

		result.Should().NotBeNull();
		result!.SoundEffects.Should().BeNullOrEmpty();
	}

	[Fact]
	public async Task BuildPublicStateAsync_BobBarker_FoldWinAtShowdown_HidesShowcaseCard()
	{
		var game = await SeedBobBarkerFoldWinAsync(nameof(Phases.Showdown), potAwarded: false);

		var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

		AssertBobBarkerFoldWinHidesPlayerCards(result);
	}

	[Fact]
	public async Task BuildPublicStateAsync_BobBarker_FoldWinAtComplete_HidesShowcaseCard()
	{
		var game = await SeedBobBarkerFoldWinAsync(nameof(Phases.Complete), potAwarded: true);

		var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

		AssertBobBarkerFoldWinHidesPlayerCards(result);
	}

	[Fact]
	public async Task BuildPublicStateAsync_BobBarker_ContestedShowdown_PublishesShowcaseCards()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BOBBARKER", 2, ante: 0);
		var game = setup.Game;
		var now = DateTimeOffset.UtcNow;

		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = "BOBBARKER";
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;

		var player0 = setup.GamePlayers[0];
		var player1 = setup.GamePlayers[1];
		BobBarkerVariantState.SetSelectedShowcaseDealOrder(player0, 5);
		BobBarkerVariantState.SetSelectedShowcaseDealOrder(player1, 5);

		AddBobBarkerHand(game.Id, player0.Id, now,
		[
			(1, CardSymbol.Ace, CardSuit.Hearts),
			(2, CardSymbol.King, CardSuit.Diamonds),
			(3, CardSymbol.Queen, CardSuit.Clubs),
			(4, CardSymbol.Jack, CardSuit.Spades),
			(5, CardSymbol.Deuce, CardSuit.Clubs)
		]);
		AddBobBarkerHand(game.Id, player1.Id, now,
		[
			(1, CardSymbol.Ace, CardSuit.Diamonds),
			(2, CardSymbol.King, CardSuit.Clubs),
			(3, CardSymbol.Queen, CardSuit.Hearts),
			(4, CardSymbol.Jack, CardSuit.Diamonds),
			(5, CardSymbol.Ten, CardSuit.Hearts)
		]);
		AddBobBarkerDealerCard(game.Id, now, CardSymbol.Jack, CardSuit.Spades);

		DbContext.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			HandNumber = 1,
			PotType = PotType.Main,
			Amount = 100,
			IsAwarded = false,
			WinReason = null,
			CreatedAt = now
		});
		await DbContext.SaveChangesAsync();

		var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

		result.Should().NotBeNull();
		result!.Showdown.Should().NotBeNull();
		result.Showdown!.WonByFold.Should().BeFalse();

		var seat0 = result.Showdown.PlayerResults.Single(p => p.SeatPosition == 0);
		var seat1 = result.Showdown.PlayerResults.Single(p => p.SeatPosition == 1);

		seat0.ShowcaseCard.Should().NotBeNull();
		seat0.ShowcaseCard!.IsFaceUp.Should().BeTrue();
		seat0.ShowcaseCard.Rank.Should().Be("2");
		seat0.ShowcaseCard.Suit.Should().Be("Clubs");
		seat0.ShowcaseCardValue.Should().NotBeNull();
		seat0.Cards.Should().NotContain(c => c.DealOrder == seat0.ShowcaseCard.DealOrder);

		seat1.ShowcaseCard.Should().NotBeNull();
		seat1.ShowcaseCard!.IsFaceUp.Should().BeTrue();
		seat1.ShowcaseCard.Rank.Should().Be("10");
		seat1.ShowcaseCard.Suit.Should().Be("Hearts");
		seat1.ShowcaseCardValue.Should().NotBeNull();
		seat1.Cards.Should().NotContain(c => c.DealOrder == seat1.ShowcaseCard.DealOrder);
	}

	[Fact]
	public async Task BuildPrivateStateAsync_BobBarker_FoldWin_OwnerSeesOwnShowcaseCard()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BOBBARKER", 3, ante: 0);
		await SeedBobBarkerFoldWinOnSetupAsync(setup, nameof(Phases.Complete), potAwarded: true);

		var ownerState = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, setup.Players[0].Email!);
		var foldedState = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, setup.Players[1].Email!);

		ownerState.Should().NotBeNull();
		ownerState!.Hand.Should().HaveCount(5);
		ownerState.Hand.Should().ContainSingle(c =>
			c.IsShowcaseCard && c.Rank == "K" && c.Suit == CardSuit.Spades.ToString());

		foldedState.Should().NotBeNull();
		foldedState!.Hand.Should().NotContain(c => c.Rank == "K" && c.Suit == CardSuit.Spades.ToString());
	}

	private async Task<Game> SeedBobBarkerFoldWinAsync(string phase, bool potAwarded)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BOBBARKER", 3, ante: 0);
		await SeedBobBarkerFoldWinOnSetupAsync(setup, phase, potAwarded);
		return setup.Game;
	}

	private async Task SeedBobBarkerFoldWinOnSetupAsync(GameSetup setup, string phase, bool potAwarded)
	{
		var game = setup.Game;
		var now = DateTimeOffset.UtcNow;

		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = "BOBBARKER";
		game.CurrentPhase = phase;
		game.Status = GameStatus.InProgress;

		setup.GamePlayers[0].HasFolded = false;
		setup.GamePlayers[1].HasFolded = true;
		setup.GamePlayers[2].HasFolded = true;
		foreach (var gamePlayer in setup.GamePlayers)
		{
			gamePlayer.Status = GamePlayerStatus.Active;
			BobBarkerVariantState.SetSelectedShowcaseDealOrder(gamePlayer, 1);
		}

		AddBobBarkerHand(game.Id, setup.GamePlayers[0].Id, now,
		[
			(1, CardSymbol.King, CardSuit.Spades),
			(2, CardSymbol.Seven, CardSuit.Diamonds),
			(3, CardSymbol.Seven, CardSuit.Clubs),
			(4, CardSymbol.Deuce, CardSuit.Hearts),
			(5, CardSymbol.Nine, CardSuit.Spades)
		]);
		AddBobBarkerHand(game.Id, setup.GamePlayers[1].Id, now,
		[
			(1, CardSymbol.Ace, CardSuit.Hearts),
			(2, CardSymbol.Queen, CardSuit.Diamonds),
			(3, CardSymbol.Jack, CardSuit.Clubs),
			(4, CardSymbol.Five, CardSuit.Hearts),
			(5, CardSymbol.Four, CardSuit.Diamonds)
		]);
		AddBobBarkerHand(game.Id, setup.GamePlayers[2].Id, now,
		[
			(1, CardSymbol.Ace, CardSuit.Clubs),
			(2, CardSymbol.Queen, CardSuit.Hearts),
			(3, CardSymbol.Jack, CardSuit.Diamonds),
			(4, CardSymbol.Six, CardSuit.Clubs),
			(5, CardSymbol.Three, CardSuit.Diamonds)
		]);
		AddBobBarkerDealerCard(game.Id, now, CardSymbol.Eight, CardSuit.Hearts);

		DbContext.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			HandNumber = 1,
			PotType = PotType.Main,
			Amount = 100,
			IsAwarded = potAwarded,
			WinReason = potAwarded ? "All others folded" : null,
			CreatedAt = now
		});
		await DbContext.SaveChangesAsync();
	}

	private void AddBobBarkerHand(
		Guid gameId,
		Guid gamePlayerId,
		DateTimeOffset dealtAt,
		(int DealOrder, CardSymbol Symbol, CardSuit Suit)[] cards)
	{
		foreach (var card in cards)
			DbContext.GameCards.Add(new GameCard
			{
				GameId = gameId,
				GamePlayerId = gamePlayerId,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = card.Suit,
				Symbol = card.Symbol,
				DealOrder = card.DealOrder,
				IsVisible = false,
				IsDiscarded = false,
				DealtAt = dealtAt,
				DealtAtPhase = nameof(Phases.Dealing)
			});
	}

	private void AddBobBarkerDealerCard(Guid gameId, DateTimeOffset dealtAt, CardSymbol symbol, CardSuit suit)
	{
		DbContext.GameCards.Add(new GameCard
		{
			GameId = gameId,
			GamePlayerId = null,
			HandNumber = 1,
			Location = CardLocation.Community,
			Suit = suit,
			Symbol = symbol,
			DealOrder = 0,
			IsVisible = false,
			IsDiscarded = false,
			DealtAt = dealtAt,
			DealtAtPhase = nameof(Phases.Dealing)
		});
	}

	private static void AssertBobBarkerFoldWinHidesPlayerCards(TableStatePublicDto? result)
	{
		result.Should().NotBeNull();
		result!.Showdown.Should().NotBeNull();
		result.Showdown!.WonByFold.Should().BeTrue();
		result.Showdown.PlayerResults.Should().NotBeEmpty();
		result.Showdown.PlayerResults.Should().OnlyContain(p =>
			p.ShowcaseCard == null && p.ShowcaseCardValue == null);
		result.Showdown.PlayerResults
			.SelectMany(p => p.Cards)
			.Should().OnlyContain(c => c.Rank == null && c.Suit == null);

		result.Seats.SelectMany(s => s.Cards).Should().OnlyContain(c => c.Rank == null && c.Suit == null);
		var seat0 = result.Seats.Single(s => s.SeatIndex == 0);
		seat0.Cards.Should().HaveCount(5);
		seat0.Cards.Should().OnlyContain(c => c.IsFaceUp == false);

		var json = JsonSerializer.Serialize(result);
		json.Should().NotContain("King");
		json.Should().NotContain("Spades");
		json.Should().Contain("Hearts");

		result.Showdown.BobBarker.Should().NotBeNull();
		result.Showdown.BobBarker!.DealerCard.Should().NotBeNull();
		result.Showdown.BobBarker.DealerCard!.IsFaceUp.Should().BeTrue();
		result.Showdown.BobBarker.DealerCard.Rank.Should().Be("8");
		result.Showdown.BobBarker.DealerCard.Suit.Should().Be("Hearts");

		result.CommunityCards.Should().ContainSingle(c =>
			c.IsFaceUp && c.Rank == "8" && c.Suit == "Hearts");
	}

	private async Task<GameSetup> SeedKingsAndLowsPlayerVsDeckAsync(
		string phase,
		bool potAwarded,
		string? winReason,
		bool includeDeckHand)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 3, ante: 0);
		var game = setup.Game;
		var now = DateTimeOffset.UtcNow;

		game.CurrentHandNumber = 1;
		game.CurrentHandGameTypeCode = "KINGSANDLOWS";
		game.CurrentPhase = phase;
		game.Status = GameStatus.InProgress;

		setup.GamePlayers[0].HasFolded = false;
		setup.GamePlayers[1].HasFolded = true;
		setup.GamePlayers[2].HasFolded = true;
		foreach (var gamePlayer in setup.GamePlayers)
			gamePlayer.Status = GamePlayerStatus.Active;

		(int DealOrder, CardSymbol Symbol, CardSuit Suit, bool IsVisible)[] playerCards =
		[
			(1, CardSymbol.Seven, CardSuit.Hearts, false),
			(2, CardSymbol.Seven, CardSuit.Diamonds, false),
			(3, CardSymbol.Deuce, CardSuit.Clubs, false),
			(4, CardSymbol.Three, CardSuit.Spades, true),
			(5, CardSymbol.Four, CardSuit.Hearts, true)
		];

		foreach (var card in playerCards)
			DbContext.GameCards.Add(new GameCard
			{
				GameId = game.Id,
				GamePlayerId = setup.GamePlayers[0].Id,
				HandNumber = 1,
				Location = CardLocation.Hand,
				Suit = card.Suit,
				Symbol = card.Symbol,
				DealOrder = card.DealOrder,
				IsVisible = card.IsVisible,
				IsDiscarded = false,
				DealtAt = now
			});

		if (includeDeckHand)
		{
			(int DealOrder, CardSymbol Symbol, CardSuit Suit)[] deckCards =
			[
				(1, CardSymbol.Jack, CardSuit.Hearts),
				(2, CardSymbol.Jack, CardSuit.Diamonds),
				(3, CardSymbol.Nine, CardSuit.Clubs),
				(4, CardSymbol.Eight, CardSuit.Spades),
				(5, CardSymbol.Five, CardSuit.Diamonds)
			];

			foreach (var card in deckCards)
				DbContext.GameCards.Add(new GameCard
				{
					GameId = game.Id,
					GamePlayerId = null,
					HandNumber = 1,
					Location = CardLocation.Board,
					Suit = card.Suit,
					Symbol = card.Symbol,
					DealOrder = card.DealOrder,
					IsVisible = true,
					IsDiscarded = false,
					DealtAt = now,
					DealtAtPhase = "PlayerVsDeck"
				});
		}

		DbContext.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			HandNumber = 1,
			PotType = PotType.Main,
			Amount = 100,
			IsAwarded = potAwarded,
			WinReason = winReason,
			CreatedAt = now
		});

		await DbContext.SaveChangesAsync();
		return setup;
	}
}