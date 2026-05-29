#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;

public class SelectShowcaseCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new SelectShowcaseCommandHandler(context);

		var result = await sut.Handle(new SelectShowcaseCommand(Guid.NewGuid(), ShowcaseCardIndex: 0, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(SelectShowcaseErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInDrawPhase_ReturnsNotInShowcasePhase()
	{
		await using var context = CreateContext();
		var game = await SeedBobBarkerGameAsync(context, currentPhase: nameof(Phases.PreFlop));
		var sut = new SelectShowcaseCommandHandler(context);

		var result = await sut.Handle(new SelectShowcaseCommand(game.Id, ShowcaseCardIndex: 0, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(SelectShowcaseErrorCode.NotInShowcasePhase);
	}

	[Fact]
	public async Task Handle_WhenRequestedPlayerSeatIsMissing_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedBobBarkerGameAsync(context);
		var sut = new SelectShowcaseCommandHandler(context);

		var result = await sut.Handle(new SelectShowcaseCommand(game.Id, ShowcaseCardIndex: 0, PlayerSeatIndex: 99), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(SelectShowcaseErrorCode.NotPlayerTurn);
	}

	[Fact]
	public async Task Handle_WhenPlayerAlreadySelectedShowcase_ReturnsAlreadySelected()
	{
		await using var context = CreateContext();
		var game = await SeedBobBarkerGameAsync(context);
		var player = await context.GamePlayers.FirstAsync(gp => gp.GameId == game.Id && gp.SeatPosition == 0);
		BobBarkerVariantState.SetSelectedShowcaseDealOrder(player, 2);
		await context.SaveChangesAsync();

		var sut = new SelectShowcaseCommandHandler(context);

		var result = await sut.Handle(new SelectShowcaseCommand(game.Id, ShowcaseCardIndex: 0, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(SelectShowcaseErrorCode.AlreadySelected);
	}

	[Fact]
	public async Task Handle_WhenPlayerHasTooFewCards_ReturnsInsufficientCards()
	{
		await using var context = CreateContext();
		var game = await SeedBobBarkerGameAsync(context, cardsPerPlayer: 4);
		var sut = new SelectShowcaseCommandHandler(context);

		var result = await sut.Handle(new SelectShowcaseCommand(game.Id, ShowcaseCardIndex: 0, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(SelectShowcaseErrorCode.InsufficientCards);
	}

	[Fact]
	public async Task Handle_WhenShowcaseCardIndexIsOutOfRange_ReturnsInvalidCardIndex()
	{
		await using var context = CreateContext();
		var game = await SeedBobBarkerGameAsync(context);
		var sut = new SelectShowcaseCommandHandler(context);

		var result = await sut.Handle(new SelectShowcaseCommand(game.Id, ShowcaseCardIndex: 5, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(SelectShowcaseErrorCode.InvalidCardIndex);
	}

	[Fact]
	public async Task Handle_WhenSecondSelectionStartsFromStaleSnapshot_CompletesDrawPhase()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		Guid gameId;

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			var game = await SeedBobBarkerGameAsync(setupContext);
			gameId = game.Id;
		}

		await using var staleContext = CreateContext(databaseName, databaseRoot);
		await using var freshContext = CreateContext(databaseName, databaseRoot);

		_ = await staleContext.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GameType)
			.Include(g => g.GameCards.Where(gc => gc.HandNumber == 1 && !gc.IsDiscarded))
			.Include(g => g.Pots)
			.FirstAsync(g => g.Id == gameId);

		var freshSut = new SelectShowcaseCommandHandler(freshContext);
		var staleSut = new SelectShowcaseCommandHandler(staleContext);

		var firstResult = await freshSut.Handle(new SelectShowcaseCommand(gameId, ShowcaseCardIndex: 0, PlayerSeatIndex: 0), CancellationToken.None);
		var secondResult = await staleSut.Handle(new SelectShowcaseCommand(gameId, ShowcaseCardIndex: 1, PlayerSeatIndex: 1), CancellationToken.None);

		firstResult.IsT0.Should().BeTrue();
		secondResult.IsT0.Should().BeTrue();

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedGame = await verificationContext.Games.FirstAsync(g => g.Id == gameId);
		var players = await verificationContext.GamePlayers
			.Where(gp => gp.GameId == gameId)
			.OrderBy(gp => gp.SeatPosition)
			.ToListAsync();

		persistedGame.CurrentPhase.Should().Be(nameof(Phases.PreFlop));
		persistedGame.CurrentDrawPlayerIndex.Should().Be(-1);
		players.Should().OnlyContain(player => player.HasDrawnThisRound);
		verificationContext.BettingRounds.Should().ContainSingle(round => round.GameId == gameId && round.Street == nameof(Phases.PreFlop));
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

	private static async Task<Game> SeedBobBarkerGameAsync(
		CardsDbContext context,
		string currentPhase = nameof(Phases.DrawPhase),
		int cardsPerPlayer = 5)
	{
		var now = DateTimeOffset.UtcNow;
		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "BOBBARKER",
			Name = "Bob Barker",
			BettingStructure = BettingStructure.Blinds,
			MinPlayers = 2,
			MaxPlayers = 8,
			InitialHoleCards = 5,
			InitialBoardCards = 0,
			MaxCommunityCards = 6,
			MaxPlayerCards = 5,
			HasDrawPhase = true,
			MaxDiscards = 1,
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

		var firstGamePlayer = CreateGamePlayer(game, firstPlayer, seatPosition: 0, now);
		var secondGamePlayer = CreateGamePlayer(game, secondPlayer, seatPosition: 1, now);

		context.Games.Add(game);
		context.Players.AddRange(firstPlayer, secondPlayer);
		context.GamePlayers.AddRange(firstGamePlayer, secondGamePlayer);

		for (var cardIndex = 0; cardIndex < cardsPerPlayer; cardIndex++)
		{
			context.GameCards.Add(CreateHoleCard(game, firstGamePlayer, cardIndex, now));
			context.GameCards.Add(CreateHoleCard(game, secondGamePlayer, cardIndex, now));
		}

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

	private static GamePlayer CreateGamePlayer(Game game, Player player, int seatPosition, DateTimeOffset now)
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
			JoinedAt = now
		};
	}

	private static GameCard CreateHoleCard(Game game, GamePlayer player, int cardIndex, DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			Location = CardLocation.Hand,
			Suit = (CardSuit)(cardIndex % 4),
			Symbol = (CardSymbol)(cardIndex + 2),
			DealOrder = cardIndex + 1,
			IsVisible = false,
			IsDiscarded = false,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}
}