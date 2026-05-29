#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;

public class ProcessDrawCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(Guid.NewGuid(), [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInDrawPhase_ReturnsNotInDrawPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.FirstBettingRound));
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.NotInDrawPhase);
	}

	[Fact]
	public async Task Handle_WhenDiscardIndexIsOutOfRange_ReturnsInvalidCardIndex()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [5]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.InvalidCardIndex);
	}

	[Fact]
	public async Task Handle_WhenPlayerHasTooFewCards_ReturnsInvalidCardIndex()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPlayerCardCount: 4);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.InvalidCardIndex);
	}

	[Fact]
	public async Task Handle_WhenTooManyDiscardsWithoutAce_ReturnsTooManyDiscards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPlayerHasAce: false);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0, 1, 2, 3]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.TooManyDiscards);
	}

	[Fact]
	public async Task Handle_WhenCurrentDrawPlayerIndexIsMissing_ReturnsNoEligiblePlayers()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentDrawPlayerIndex: -1);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, []), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.NoEligiblePlayers);
	}

	[Fact]
	public async Task Handle_WhenCurrentDrawPlayerSeatIsMissing_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentDrawPlayerIndex: 99);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, []), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.NotPlayerTurn);
	}

	[Fact]
	public async Task Handle_WhenDeckDoesNotContainEnoughReplacementCards_ReturnsInsufficientDeckCards()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, deckCardCount: 1);
		var sut = new ProcessDrawCommandHandler(context);

		var result = await sut.Handle(new ProcessDrawCommand(game.Id, [0, 1]), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessDrawErrorCode.InsufficientDeckCards);
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Game> SeedGameAsync(
		CardsDbContext context,
		string currentPhase = nameof(Phases.DrawPhase),
		int currentDrawPlayerIndex = 0,
		int currentPlayerCardCount = 5,
		bool currentPlayerHasAce = false,
		int deckCardCount = 5)
	{
		var now = DateTimeOffset.UtcNow;

		var game = new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentDrawPlayerIndex = currentDrawPlayerIndex,
			CurrentPlayerIndex = currentDrawPlayerIndex,
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

		var firstPlayerSymbols = currentPlayerHasAce
			? new[] { CardSymbol.Ace, CardSymbol.Three, CardSymbol.Four, CardSymbol.Five, CardSymbol.Six }
			: new[] { CardSymbol.Deuce, CardSymbol.Three, CardSymbol.Four, CardSymbol.Five, CardSymbol.Six };

		for (var cardIndex = 0; cardIndex < currentPlayerCardCount; cardIndex++)
		{
			context.GameCards.Add(CreatePlayerCard(game, firstGamePlayer, cardIndex, firstPlayerSymbols[cardIndex], now));
		}

		for (var cardIndex = 0; cardIndex < 5; cardIndex++)
		{
			context.GameCards.Add(CreatePlayerCard(game, secondGamePlayer, cardIndex, (CardSymbol)(cardIndex + 8), now));
		}

		for (var cardIndex = 0; cardIndex < deckCardCount; cardIndex++)
		{
			context.GameCards.Add(CreateDeckCard(game, cardIndex, now));
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
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}

	private static GameCard CreatePlayerCard(
		Game game,
		GamePlayer player,
		int cardIndex,
		CardSymbol symbol,
		DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = cardIndex + 1,
			Location = CardLocation.Hole,
			IsVisible = false,
			Symbol = symbol,
			Suit = (CardSuit)(cardIndex % 4),
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}

	private static GameCard CreateDeckCard(Game game, int cardIndex, DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = null,
			HandNumber = game.CurrentHandNumber,
			DealOrder = 100 + cardIndex,
			Location = CardLocation.Deck,
			IsVisible = false,
			Symbol = (CardSymbol)(cardIndex + 9),
			Suit = (CardSuit)(cardIndex % 4),
			DealtAt = now,
			DealtAtPhase = nameof(Phases.Dealing)
		};
	}
}