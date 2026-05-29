#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.AceChoice;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using static CardGames.Poker.Api.Features.Games.InBetween.InBetweenVariantState;
using PotEntity = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.InBetween.v1.Commands.AceChoice;

public class AceChoiceCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new AceChoiceCommandHandler(context);

		var result = await sut.Handle(new AceChoiceCommand(Guid.NewGuid(), Guid.NewGuid(), AceIsHigh: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(AceChoiceErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInInBetweenTurnPhase_ReturnsInvalidPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.PreFlop));
		var playerId = await context.GamePlayers
			.Where(gamePlayer => gamePlayer.GameId == game.Id && gamePlayer.SeatPosition == 0)
			.Select(gamePlayer => gamePlayer.PlayerId)
			.SingleAsync();
		var sut = new AceChoiceCommandHandler(context);

		var result = await sut.Handle(new AceChoiceCommand(game.Id, playerId, AceIsHigh: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(AceChoiceErrorCode.InvalidPhase);
		result.AsT1.Message.Should().Contain(nameof(Phases.PreFlop));
	}

	[Fact]
	public async Task Handle_WhenPlayerIsMissing_ReturnsPlayerNotFound()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new AceChoiceCommandHandler(context);

		var result = await sut.Handle(new AceChoiceCommand(game.Id, Guid.NewGuid(), AceIsHigh: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(AceChoiceErrorCode.PlayerNotFound);
	}

	[Fact]
	public async Task Handle_WhenItIsNotPlayersTurn_ReturnsNotPlayersTurn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPlayerIndex: 1);
		var playerId = await context.GamePlayers
			.Where(gamePlayer => gamePlayer.GameId == game.Id && gamePlayer.SeatPosition == 0)
			.Select(gamePlayer => gamePlayer.PlayerId)
			.SingleAsync();
		var sut = new AceChoiceCommandHandler(context);

		var result = await sut.Handle(new AceChoiceCommand(game.Id, playerId, AceIsHigh: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(AceChoiceErrorCode.NotPlayersTurn);
	}

	[Fact]
	public async Task Handle_WhenAceChoiceIsNotRequired_ReturnsAceChoiceNotRequired()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, subPhase: TurnSubPhase.AwaitingBetOrPass);
		var playerId = await context.GamePlayers
			.Where(gamePlayer => gamePlayer.GameId == game.Id && gamePlayer.SeatPosition == 0)
			.Select(gamePlayer => gamePlayer.PlayerId)
			.SingleAsync();
		var sut = new AceChoiceCommandHandler(context);

		var result = await sut.Handle(new AceChoiceCommand(game.Id, playerId, AceIsHigh: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(AceChoiceErrorCode.AceChoiceNotRequired);
	}

	[Fact]
	public async Task Handle_WhenAwaitingAceChoice_PersistsChoiceAndAdvancesSubPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var playerId = await context.GamePlayers
			.Where(gamePlayer => gamePlayer.GameId == game.Id && gamePlayer.SeatPosition == 0)
			.Select(gamePlayer => gamePlayer.PlayerId)
			.SingleAsync();
		var sut = new AceChoiceCommandHandler(context);

		var result = await sut.Handle(new AceChoiceCommand(game.Id, playerId, AceIsHigh: false), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.AceIsHigh.Should().BeFalse();
		result.AsT0.NextSubPhase.Should().Be(TurnSubPhase.AwaitingBetOrPass.ToString());

		var persistedGame = await context.Games.SingleAsync(persisted => persisted.Id == game.Id);
		var persistedState = GetState(persistedGame);
		persistedState.AceIsHigh.Should().BeFalse();
		persistedState.SubPhase.Should().Be(TurnSubPhase.AwaitingBetOrPass);
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
		string currentPhase = nameof(Phases.InBetweenTurn),
		int currentPlayerIndex = 0,
		TurnSubPhase subPhase = TurnSubPhase.AwaitingAceChoice)
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

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(alice, bob);
		context.GamePlayers.AddRange(
			CreateGamePlayer(game, alice, seatPosition: 0, now),
			CreateGamePlayer(game, bob, seatPosition: 1, now));
		context.Pots.Add(new PotEntity
		{
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 50,
			CreatedAt = now
		});

		SetState(game, new InBetweenState
		{
			SubPhase = subPhase,
			PlayersCompletedFirstTurn = []
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
			CurrentBet = 0,
			Status = GamePlayerStatus.Active,
			JoinedAt = now
		};
	}
}