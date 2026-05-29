#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.IrishHoldEm.v1.Commands.ProcessDiscard;

public class ProcessDiscardCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(Guid.NewGuid(), [0, 1]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInDrawPhase_ReturnsNotInDiscardPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.PreFlop));
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(game.Id, [0, 1]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.NotInDiscardPhase);
	}

	[Fact]
	public async Task Handle_WhenDiscardCountIsInvalid_ReturnsInvalidDiscardCount()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(game.Id, [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.InvalidDiscardCount);
	}

	[Fact]
	public async Task Handle_WhenRequestedSeatIsMissing_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(game.Id, [0, 1], PlayerSeatIndex: 99), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.NotPlayerTurn);
	}

	[Fact]
	public async Task Handle_WhenPlayerHasTooFewCards_ReturnsInsufficientCards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, firstPlayerCardCount: 3);
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(game.Id, [0, 1]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.InsufficientCards);
	}

	[Fact]
	public async Task Handle_WhenDiscardIndexIsOutOfRange_ReturnsInvalidCardIndex()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(game.Id, [0, 4]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.InvalidCardIndex);
	}

	[Fact]
	public async Task Handle_WhenDiscardIndicesContainDuplicates_ReturnsInvalidCardIndex()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(game.Id, [1, 1]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.InvalidCardIndex);
	}

	[Fact]
	public async Task Handle_WhenNoEligiblePlayersRemain_ReturnsNoEligiblePlayers()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, firstPlayerHasDrawn: true, secondPlayerHasDrawn: true);
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(game.Id, [0, 1]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.NoEligiblePlayers);
	}

	[Fact]
	public async Task Handle_WhenPlayerAlreadyDiscarded_ReturnsAlreadyDiscarded()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, firstPlayerHasDrawn: true, secondPlayerHasDrawn: false);
		var sut = new ProcessDiscardCommandHandler(context);

		var result = await sut.Handle(new ProcessDiscardCommand(game.Id, [0, 1], PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDiscardErrorCode.AlreadyDiscarded);
	}

	[Fact]
	public async Task Handle_WhenSecondDiscardStartsFromStaleSnapshot_AdvancesToTurn()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		Guid gameId;

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			var game = await SeedGameAsync(setupContext);
			gameId = game.Id;
		}

		await using var staleContext = CreateContext(databaseName, databaseRoot);
		await using var freshContext = CreateContext(databaseName, databaseRoot);

		_ = await staleContext.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameType)
			.Include(g => g.GameCards.Where(gc => gc.HandNumber == 1 && !gc.IsDiscarded))
			.FirstAsync(g => g.Id == gameId);

		var freshSut = new ProcessDiscardCommandHandler(freshContext);
		var staleSut = new ProcessDiscardCommandHandler(staleContext);

		var firstResult = await freshSut.Handle(new ProcessDiscardCommand(gameId, [0, 1], PlayerSeatIndex: 0), CancellationToken.None);
		var secondResult = await staleSut.Handle(new ProcessDiscardCommand(gameId, [0, 1], PlayerSeatIndex: 1), CancellationToken.None);

		firstResult.IsT0.Should().BeTrue();
		secondResult.IsT0.Should().BeTrue();

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedGame = await verificationContext.Games.FirstAsync(g => g.Id == gameId);
		var players = await verificationContext.GamePlayers
			.Where(gp => gp.GameId == gameId)
			.OrderBy(gp => gp.SeatPosition)
			.ToListAsync();
		var communityCards = await verificationContext.GameCards
			.Where(gc => gc.GameId == gameId && gc.Location == CardLocation.Community)
			.OrderBy(gc => gc.DealOrder)
			.ToListAsync();

		persistedGame.CurrentPhase.Should().Be(nameof(Phases.Turn));
		persistedGame.CurrentDrawPlayerIndex.Should().Be(-1);
		persistedGame.CurrentPlayerIndex.Should().Be(1);
		players.Should().OnlyContain(player => player.HasDrawnThisRound);
		communityCards.Should().ContainSingle();
		communityCards[0].DealtAtPhase.Should().Be(nameof(Phases.Turn));
		verificationContext.BettingRounds.Should().ContainSingle(round => round.GameId == gameId && round.Street == nameof(Phases.Turn));
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

	private static async Task<Game> SeedGameAsync(
		CardsDbContext context,
		string currentPhase = nameof(Phases.DrawPhase),
		string gameTypeCode = PokerGameMetadataRegistry.IrishHoldEmCode,
		int firstPlayerCardCount = 4,
		int secondPlayerCardCount = 4,
		bool firstPlayerHasDrawn = false,
		bool secondPlayerHasDrawn = false)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = gameTypeCode,
			Name = "Irish Hold 'Em",
			BettingStructure = BettingStructure.Blinds,
			MinPlayers = 2,
			MaxPlayers = 10,
			InitialHoleCards = 4,
			InitialBoardCards = 0,
			MaxCommunityCards = 5,
			MaxPlayerCards = 4,
			HasDrawPhase = true,
			MaxDiscards = 2,
			CreatedAt = now,
			UpdatedAt = now
		};

		var game = new Game
		{
			Id = Guid.NewGuid(),
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentHandGameTypeCode = gameTypeCode,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentDrawPlayerIndex = 0,
			CurrentPlayerIndex = 0,
			DealerPosition = 0,
			MinBet = 10,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		var firstPlayer = CreatePlayer("Alice", now);
		var secondPlayer = CreatePlayer("Bob", now);

		var firstGamePlayer = CreateGamePlayer(game, firstPlayer, seatPosition: 0, firstPlayerHasDrawn, now);
		var secondGamePlayer = CreateGamePlayer(game, secondPlayer, seatPosition: 1, secondPlayerHasDrawn, now);

		context.Games.Add(game);
		context.Players.AddRange(firstPlayer, secondPlayer);
		context.GamePlayers.AddRange(firstGamePlayer, secondGamePlayer);

		SeedHoleCards(context, game, firstGamePlayer, firstPlayerCardCount, now);
		SeedHoleCards(context, game, secondGamePlayer, secondPlayerCardCount, now, dealOrderOffset: 10);
		SeedDeckCards(context, game, 3, now);

		await context.SaveChangesAsync();
		return game;
	}

	private static void SeedHoleCards(
		CardsDbContext context,
		Game game,
		GamePlayer player,
		int count,
		DateTimeOffset now,
		int dealOrderOffset = 0)
	{
		for (var cardIndex = 0; cardIndex < count; cardIndex++)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = player.Id,
				GamePlayer = player,
				HandNumber = game.CurrentHandNumber,
				DealOrder = dealOrderOffset + cardIndex + 1,
				Location = CardLocation.Hole,
				IsVisible = false,
				Symbol = (CardSymbol)(cardIndex + 1),
				Suit = (CardSuit)(cardIndex % 4),
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}
	}

	private static void SeedDeckCards(CardsDbContext context, Game game, int count, DateTimeOffset now)
	{
		for (var cardIndex = 0; cardIndex < count; cardIndex++)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				DealOrder = 100 + cardIndex,
				Location = CardLocation.Deck,
				IsVisible = false,
				Symbol = (CardSymbol)(cardIndex + 7),
				Suit = (CardSuit)(cardIndex % 4),
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}
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

	private static GamePlayer CreateGamePlayer(Game game, Player player, int seatPosition, bool hasDrawnThisRound, DateTimeOffset now)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seatPosition,
			ChipStack = 100,
			StartingChips = 100,
			Status = GamePlayerStatus.Active,
			HasDrawnThisRound = hasDrawnThisRound,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}
}