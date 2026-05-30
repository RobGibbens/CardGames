#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;

public class KeepOrTradeCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = CreateSut(context);

		var result = await sut.Handle(new KeepOrTradeCommand(Guid.NewGuid(), Guid.NewGuid(), "Keep"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInKeepOrTradePhase_ReturnsInvalidPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.PreFlop));
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new KeepOrTradeCommand(game.Id, playerId, "Keep"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.InvalidPhase);
		result.AsT1.Message.Should().Contain(nameof(Phases.PreFlop));
	}

	[Fact]
	public async Task Handle_WhenPlayerIsMissing_ReturnsPlayerNotFound()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = CreateSut(context);

		var result = await sut.Handle(new KeepOrTradeCommand(game.Id, Guid.NewGuid(), "Keep"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.PlayerNotFound);
	}

	[Fact]
	public async Task Handle_WhenItIsNotPlayersTurn_ReturnsNotPlayersTurn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPlayerIndex: 1);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new KeepOrTradeCommand(game.Id, playerId, "Keep"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.NotPlayersTurn);
	}

	[Fact]
	public async Task Handle_WhenDecisionIsInvalid_ReturnsInvalidDecision()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new KeepOrTradeCommand(game.Id, playerId, "Maybe"), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.InvalidDecision);
	}

	[Fact]
	public async Task Handle_WhenTradeTargetHasNoHandCard_ReturnsBlockedWithoutTrade()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			playerCount: 2,
			currentPlayerIndex: 0,
			dealerPosition: 1,
			handCardsBySeat: new Dictionary<int, CardSymbol>
			{
				[0] = CardSymbol.Four
			},
			deckSymbols: [CardSymbol.Ace]);
		var playerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 0);
		var sut = CreateSut(context);

		var result = await sut.Handle(new KeepOrTradeCommand(game.Id, playerId, "Trade"), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.DidTrade.Should().BeFalse();
		result.AsT0.WasBlocked.Should().BeTrue();
		result.AsT0.NextPhase.Should().Be(nameof(Phases.KeepOrTrade));
		result.AsT0.NextPlayerSeatIndex.Should().Be(1);

		var persistedPlayer = await context.GamePlayers.SingleAsync(gamePlayer => gamePlayer.GameId == game.Id && gamePlayer.PlayerId == playerId);
		persistedPlayer.VariantState.Should().Be("SYN_TRADED");
	}

	[Fact]
	public async Task Handle_WhenDealerHasNoHandCard_CompletesWithoutTrade()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			playerCount: 2,
			currentPlayerIndex: 1,
			dealerPosition: 1,
			handCardsBySeat: new Dictionary<int, CardSymbol>
			{
				[0] = CardSymbol.Four
			},
			deckSymbols: [CardSymbol.King]);
		var dealerPlayerId = await GetPlayerIdAsync(context, game.Id, seatPosition: 1);
		var sut = CreateSut(context, out var flowHandler, out var handSettlementService);

		flowHandler.PerformShowdownAsync(
			Arg.Any<CardsDbContext>(),
			Arg.Any<Game>(),
			Arg.Any<IHandHistoryRecorder>(),
			Arg.Any<DateTimeOffset>(),
			Arg.Any<CancellationToken>())
			.Returns(ShowdownResult.Success([], [], 0));
		flowHandler.ProcessPostShowdownAsync(
			Arg.Any<CardsDbContext>(),
			Arg.Any<Game>(),
			Arg.Any<ShowdownResult>(),
			Arg.Any<DateTimeOffset>(),
			Arg.Any<CancellationToken>())
			.Returns(nameof(Phases.Complete));

		var result = await sut.Handle(new KeepOrTradeCommand(game.Id, dealerPlayerId, "Trade"), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.DidTrade.Should().BeFalse();
		result.AsT0.WasBlocked.Should().BeFalse();
		result.AsT0.NextPhase.Should().Be(nameof(Phases.Complete));
		result.AsT0.NextPlayerSeatIndex.Should().BeNull();

		await flowHandler.Received(1).PerformShowdownAsync(
			Arg.Any<CardsDbContext>(),
			Arg.Any<Game>(),
			Arg.Any<IHandHistoryRecorder>(),
			Arg.Any<DateTimeOffset>(),
			Arg.Any<CancellationToken>());
		await handSettlementService.Received(1).SettleHandAsync(
			Arg.Any<Game>(),
			Arg.Is<Dictionary<string, int>>(payouts => payouts.Count == 0),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task Handle_WhenDuplicateDecisionStartsFromStaleSnapshot_RejectsStaleTurnAndKeepsFirstTrade()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		Guid gameId;
		Guid playerId;

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			var game = await SeedGameAsync(
				setupContext,
				playerCount: 3,
				currentPlayerIndex: 0,
				dealerPosition: 2,
				handCardsBySeat: new Dictionary<int, CardSymbol>
				{
					[0] = CardSymbol.Four,
					[1] = CardSymbol.Six,
					[2] = CardSymbol.Eight
				},
				deckSymbols: [CardSymbol.Ace]);
			gameId = game.Id;
			playerId = await GetPlayerIdAsync(setupContext, game.Id, seatPosition: 0);
		}

		await using var staleContext = CreateContext(databaseName, databaseRoot);
		await using var freshContext = CreateContext(databaseName, databaseRoot);

		_ = await staleContext.Games
			.Include(game => game.GamePlayers)
				.ThenInclude(gamePlayer => gamePlayer.Player)
			.Include(game => game.GameCards)
			.FirstAsync(game => game.Id == gameId);

		var freshSut = CreateSut(freshContext);
		var staleSut = CreateSut(staleContext);

		var firstResult = await freshSut.Handle(new KeepOrTradeCommand(gameId, playerId, "Trade"), CancellationToken.None);
		var secondResult = await staleSut.Handle(new KeepOrTradeCommand(gameId, playerId, "Keep"), CancellationToken.None);

		firstResult.IsT0.Should().BeTrue();
		firstResult.AsT0.DidTrade.Should().BeTrue();
		secondResult.IsT1.Should().BeTrue();
		secondResult.AsT1.Code.Should().Be(KeepOrTradeErrorCode.NotPlayersTurn);

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedGame = await verificationContext.Games.FirstAsync(game => game.Id == gameId);
		var players = await verificationContext.GamePlayers
			.Where(gamePlayer => gamePlayer.GameId == gameId)
			.OrderBy(gamePlayer => gamePlayer.SeatPosition)
			.ToListAsync();
		var handCards = await verificationContext.GameCards
			.Where(gameCard => gameCard.GameId == gameId &&
			                  gameCard.HandNumber == persistedGame.CurrentHandNumber &&
			                  gameCard.Location == CardLocation.Hand &&
			                  !gameCard.IsDiscarded)
			.ToListAsync();

		persistedGame.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));
		persistedGame.CurrentPlayerIndex.Should().Be(1);
		players[0].VariantState.Should().Be("SYN_TRADED");
		handCards.Should().ContainSingle(gameCard => gameCard.GamePlayerId == players[0].Id && gameCard.Symbol == CardSymbol.Six);
		handCards.Should().ContainSingle(gameCard => gameCard.GamePlayerId == players[1].Id && gameCard.Symbol == CardSymbol.Four);
	}

	private static KeepOrTradeCommandHandler CreateSut(CardsDbContext context)
	{
		return CreateSut(context, out _, out _);
	}

	private static KeepOrTradeCommandHandler CreateSut(
		CardsDbContext context,
		out IGameFlowHandler flowHandler,
		out IHandSettlementService handSettlementService)
	{
		flowHandler = Substitute.For<IGameFlowHandler>();
		var flowHandlerFactory = Substitute.For<IGameFlowHandlerFactory>();
		flowHandlerFactory.GetHandler(Arg.Any<string?>()).Returns(flowHandler);
		handSettlementService = Substitute.For<IHandSettlementService>();

		return new KeepOrTradeCommandHandler(
			context,
			flowHandlerFactory,
			Substitute.For<IHandHistoryRecorder>(),
			handSettlementService);
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
		int playerCount = 3,
		int currentPlayerIndex = 0,
		int dealerPosition = 2,
		string currentPhase = nameof(Phases.KeepOrTrade),
		IReadOnlyDictionary<int, CardSymbol>? handCardsBySeat = null,
		IReadOnlyList<CardSymbol>? deckSymbols = null)
	{
		var now = DateTimeOffset.UtcNow;
		handCardsBySeat ??= new Dictionary<int, CardSymbol>
		{
			[0] = CardSymbol.Four,
			[1] = CardSymbol.Six,
			[2] = CardSymbol.Eight
		};
		deckSymbols ??= [CardSymbol.Ace];

		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentHandGameTypeCode = PokerGameMetadataRegistry.ScrewYourNeighborCode,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentPlayerIndex = currentPlayerIndex,
			DealerPosition = dealerPosition,
			Status = GameStatus.InProgress,
			CreatedAt = now,
			UpdatedAt = now
		};

		context.Games.Add(game);

		for (var seatPosition = 0; seatPosition < playerCount; seatPosition++)
		{
			var player = new Player
			{
				Id = Guid.NewGuid(),
				Name = $"Player {seatPosition + 1}",
				CreatedAt = now,
				UpdatedAt = now
			};

			var gamePlayer = new GamePlayer
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
				JoinedAt = now,
				RowVersion = [1, 2, 3]
			};

			context.Players.Add(player);
			context.GamePlayers.Add(gamePlayer);

			if (handCardsBySeat.TryGetValue(seatPosition, out var symbol))
			{
				context.GameCards.Add(new GameCard
				{
					Id = Guid.NewGuid(),
					GameId = game.Id,
					Game = game,
					GamePlayerId = gamePlayer.Id,
					GamePlayer = gamePlayer,
					HandNumber = game.CurrentHandNumber,
					DealOrder = seatPosition + 1,
					Location = CardLocation.Hand,
					IsVisible = false,
					IsDiscarded = false,
					Symbol = symbol,
					Suit = (CardSuit)(seatPosition % 4),
					DealtAt = now,
					DealtAtPhase = nameof(Phases.Dealing)
				});
			}
		}

		for (var deckIndex = 0; deckIndex < deckSymbols.Count; deckIndex++)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				DealOrder = 100 + deckIndex,
				Location = CardLocation.Deck,
				IsVisible = false,
				IsDiscarded = false,
				Symbol = deckSymbols[deckIndex],
				Suit = (CardSuit)(deckIndex % 4),
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}

		await context.SaveChangesAsync();
		return game;
	}
}