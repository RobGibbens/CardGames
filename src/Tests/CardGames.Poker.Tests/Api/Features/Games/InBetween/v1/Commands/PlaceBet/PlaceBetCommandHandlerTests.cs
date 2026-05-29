#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Core.French.Cards;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.PlaceBet;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;
using static CardGames.Poker.Api.Features.Games.InBetween.InBetweenVariantState;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.InBetween.v1.Commands.PlaceBet;

public class PlaceBetCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(Guid.NewGuid(), Guid.NewGuid(), Amount: 10), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInInBetweenTurnPhase_ReturnsInvalidPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.PreFlop));
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, playerId, Amount: 10), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.InvalidPhase);
		result.AsT1.Message.Should().Contain(nameof(Phases.PreFlop));
	}

	[Fact]
	public async Task Handle_WhenPlayerIsMissing_ReturnsPlayerNotFound()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, Guid.NewGuid(), Amount: 10), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.PlayerNotFound);
	}

	[Fact]
	public async Task Handle_WhenItIsNotPlayersTurn_ReturnsNotPlayersTurn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPlayerIndex: 1);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, playerId, Amount: 10), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.NotPlayersTurn);
	}

	[Fact]
	public async Task Handle_WhenAceChoiceIsStillRequired_ReturnsAceChoiceRequired()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, subPhase: TurnSubPhase.AwaitingAceChoice);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, playerId, Amount: 10), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.AceChoiceRequired);
	}

	[Fact]
	public async Task Handle_WhenBetAmountIsNegative_ReturnsInvalidBetAmount()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, playerId, Amount: -1), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.InvalidBetAmount);
		result.AsT1.Message.Should().Contain("cannot be negative");
	}

	[Fact]
	public async Task Handle_WhenBetExceedsPot_ReturnsBetExceedsPot()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, potAmount: 25, playerChips: 100);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, playerId, Amount: 30), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.BetExceedsPot);
	}

	[Fact]
	public async Task Handle_WhenBetExceedsChipStack_ReturnsBetExceedsChips()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, potAmount: 100, playerChips: 40);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, playerId, Amount: 50), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.BetExceedsChips);
	}

	[Fact]
	public async Task Handle_WhenPlayerHasNoChipsAndBets_ReturnsInvalidBetAmount()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, potAmount: 100, playerChips: 0);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, playerId, Amount: 1), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.InvalidBetAmount);
		result.AsT1.Message.Should().Contain("must pass");
	}

	[Fact]
	public async Task Handle_WhenFullPotBetIsPlacedDuringFirstOrbit_ReturnsFullPotNotAllowedFirstOrbit()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, potAmount: 60, playersCompletedFirstTurn: [1]);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new PlaceBetCommand(game.Id, playerId, Amount: 60), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PlaceBetErrorCode.FullPotNotAllowedFirstOrbit);
	}

	[Fact]
	public async Task Handle_WhenBoundaryDealStartsFromStaleSnapshot_LeavesSingleBoundaryPairPersisted()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		Guid gameId;
		Guid playerId;

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			var game = await SeedGameAsync(
				setupContext,
				subPhase: TurnSubPhase.AwaitingFirstBoundary,
				deckCards:
				[
					(CardSuit.Hearts, CardSymbol.Five),
					(CardSuit.Spades, CardSymbol.Jack),
					(CardSuit.Clubs, CardSymbol.Queen),
					(CardSuit.Diamonds, CardSymbol.Ten)
				]);
			gameId = game.Id;
			playerId = await GetPlayerIdAsync(setupContext, game.Id, seatPosition: 0);
		}

		await using var staleContext = CreateContext(databaseName, databaseRoot);
		await using var freshContext = CreateContext(databaseName, databaseRoot);

		_ = await staleContext.Games
			.Include(game => game.GamePlayers)
				.ThenInclude(gamePlayer => gamePlayer.Player)
			.Include(game => game.GameCards)
			.Include(game => game.Pots)
			.FirstAsync(game => game.Id == gameId);

		var freshSut = CreateSut(freshContext);
		var staleSut = CreateSut(staleContext);

		var firstResult = await freshSut.Handle(new PlaceBetCommand(gameId, playerId, Amount: 10), CancellationToken.None);
		var secondResult = await staleSut.Handle(new PlaceBetCommand(gameId, playerId, Amount: 10), CancellationToken.None);

		firstResult.IsT0.Should().BeTrue();
		secondResult.IsT0.Should().BeTrue();
		firstResult.AsT0.TurnResult.Should().Be("BoundaryCardsDealt");
		secondResult.AsT0.TurnResult.Should().Be("BoundaryCardsDealt");

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedGame = await verificationContext.Games.SingleAsync(game => game.Id == gameId);
		var persistedState = GetState(persistedGame);
		var communityCards = await verificationContext.GameCards
			.Where(gameCard => gameCard.GameId == gameId && gameCard.Location == CardLocation.Community && !gameCard.IsDiscarded)
			.OrderBy(gameCard => gameCard.DealOrder)
			.ToListAsync();

		persistedState.SubPhase.Should().Be(TurnSubPhase.AwaitingBetOrPass);
		communityCards.Should().HaveCount(2);
		communityCards.Select(gameCard => gameCard.Id).Should().OnlyHaveUniqueItems();
		communityCards.Select(gameCard => gameCard.Symbol).Should().Equal(CardSymbol.Five, CardSymbol.Jack);
	}

	private static PlaceBetCommandHandler CreateSut(CardsDbContext context)
	{
		return new PlaceBetCommandHandler(
			context,
			Substitute.For<IGameFlowHandlerFactory>(),
			Substitute.For<IHandHistoryRecorder>());
	}

	private static CardsDbContext CreateContext()
	{
		return CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Guid> GetPlayerIdAsync(CardsDbContext context, Guid gameId, int seatPosition)
	{
		return await context.GamePlayers
			.Where(gamePlayer => gamePlayer.GameId == gameId && gamePlayer.SeatPosition == seatPosition)
			.Select(gamePlayer => gamePlayer.PlayerId)
			.SingleAsync();
	}

	private static async Task<Game> SeedGameAsync(
		CardsDbContext context,
		string currentPhase = nameof(Phases.InBetweenTurn),
		int currentPlayerIndex = 0,
		TurnSubPhase subPhase = TurnSubPhase.AwaitingBetOrPass,
		int potAmount = 50,
		int playerChips = 100,
		int[]? playersCompletedFirstTurn = null,
		(CardSuit Suit, CardSymbol Symbol)[]? deckCards = null)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "INBETWEEN",
			Name = "In-Between",
			BettingStructure = BettingStructure.Ante,
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 0,
			InitialBoardCards = 0,
			MaxCommunityCards = 3,
			MaxPlayerCards = 0,
			HasDrawPhase = false,
			MaxDiscards = 0,
			CreatedAt = now,
			UpdatedAt = now
		};

		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentHandGameTypeCode = gameType.Code,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentPlayerIndex = currentPlayerIndex,
			DealerPosition = 1,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = CreatePlayer("Alice", now);
		var bob = CreatePlayer("Bob", now);

		var aliceGamePlayer = CreateGamePlayer(game, alice, seatPosition: 0, chipStack: playerChips, now);
		var bobGamePlayer = CreateGamePlayer(game, bob, seatPosition: 1, chipStack: 100, now);

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(aliceGamePlayer, bobGamePlayer);
		context.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = potAmount,
			CreatedAt = now
		});

		var orderedDeckCards = deckCards ??
		[
			(CardSuit.Hearts, CardSymbol.Five),
			(CardSuit.Spades, CardSymbol.Jack),
			(CardSuit.Clubs, CardSymbol.Queen),
			(CardSuit.Diamonds, CardSymbol.Ten),
			(CardSuit.Hearts, CardSymbol.King)
		];

		for (var deckOrder = 0; deckOrder < orderedDeckCards.Length; deckOrder++)
		{
			var (suit, symbol) = orderedDeckCards[deckOrder];
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				HandNumber = game.CurrentHandNumber,
				Suit = suit,
				Symbol = symbol,
				DealOrder = deckOrder,
				Location = CardLocation.Deck,
				IsVisible = false,
				IsDiscarded = false,
				DealtAt = now
			});
		}

		SetState(game, new InBetweenState
		{
			SubPhase = subPhase,
			PlayersCompletedFirstTurn = playersCompletedFirstTurn?.ToHashSet() ?? []
		});

		await context.SaveChangesAsync();
		return game;
	}

	private static Player CreatePlayer(string name, DateTimeOffset now)
	{
		return new Player
		{
			Id = Guid.NewGuid(),
			Name = name,
			CreatedAt = now,
			UpdatedAt = now
		};
	}

	private static GamePlayer CreateGamePlayer(Game game, Player player, int seatPosition, int chipStack, DateTimeOffset now)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seatPosition,
			ChipStack = chipStack,
			StartingChips = chipStack,
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now
		};
	}
}