#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Phases = CardGames.Poker.Betting.Phases;

namespace CardGames.Poker.Tests.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn;

public class GetCurrentPlayerTurnQueryHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsNull()
	{
		await using var context = CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
		using var serviceProvider = CreateServiceProvider();
		var sut = new CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn.GetCurrentPlayerTurnQueryHandler(
			context,
			serviceProvider.GetRequiredService<HybridCache>());

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(Guid.NewGuid()), CancellationToken.None);

		response.Should().BeNull();
	}

	[Fact]
	public async Task Handle_WhenCurrentPlayerIndexIsNegative_ReturnsNull()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = await SeedQueryGameAsync(databaseName, databaseRoot, currentPlayerIndex: -1);
		using var serviceProvider = CreateServiceProvider();

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn.GetCurrentPlayerTurnQueryHandler(
			context,
			serviceProvider.GetRequiredService<HybridCache>());

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(gameId), CancellationToken.None);

		response.Should().BeNull();
	}

	[Fact]
	public async Task Handle_WhenCurrentPlayerSeatIsMissing_ReturnsNull()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = await SeedQueryGameAsync(databaseName, databaseRoot, currentPlayerIndex: 4);
		using var serviceProvider = CreateServiceProvider();

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn.GetCurrentPlayerTurnQueryHandler(
			context,
			serviceProvider.GetRequiredService<HybridCache>());

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(gameId), CancellationToken.None);

		response.Should().BeNull();
	}

	[Fact]
	public async Task Handle_ConcurrentRequests_ReturnConsistentResults()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = await SeedQueryGameAsync(databaseName, databaseRoot);
		using var serviceProvider = CreateServiceProvider();
		var query = new GetCurrentPlayerTurnQuery(gameId);

		var results = await Task.WhenAll(Enumerable.Range(0, 20)
			.Select(async _ =>
			{
				await using var context = CreateContext(databaseName, databaseRoot);
				var sut = new CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn.GetCurrentPlayerTurnQueryHandler(
					context,
					serviceProvider.GetRequiredService<HybridCache>());
				return await sut.Handle(query, CancellationToken.None);
			}));

		results.Should().OnlyContain(result => result != null);
		results.Should().AllSatisfy(result => result.Should().BeEquivalentTo(results[0]));
		results[0]!.Player.PlayerName.Should().Be("Alice");
		results[0]!.AvailableActions.Should().NotBeNull();
		results[0]!.AvailableActions!.CanCheck.Should().BeTrue();
		results[0]!.HandOdds.Should().NotBeNull();
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static ServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		#pragma warning disable EXTEXP0018
		services.AddHybridCache();
		#pragma warning restore EXTEXP0018
		return services.BuildServiceProvider();
	}

	private static async Task<Guid> SeedQueryGameAsync(
		string databaseName,
		InMemoryDatabaseRoot databaseRoot,
		int currentPlayerIndex = 0)
	{
		var now = DateTimeOffset.UtcNow;
		var gameId = Guid.NewGuid();
		var playerId = Guid.NewGuid();
		var gamePlayerId = Guid.NewGuid();

		await using var context = CreateContext(databaseName, databaseRoot);

		var game = new Game
		{
			Id = gameId,
			CurrentPhase = nameof(Phases.FirstBettingRound),
			CurrentHandNumber = 1,
			CurrentPlayerIndex = currentPlayerIndex,
			CreatedAt = now,
			UpdatedAt = now
		};

		var player = new Player
		{
			Id = playerId,
			Name = "Alice",
			CreatedAt = now,
			UpdatedAt = now
		};

		var gamePlayer = new GamePlayer
		{
			Id = gamePlayerId,
			GameId = gameId,
			Game = game,
			PlayerId = playerId,
			Player = player,
			SeatPosition = 0,
			ChipStack = 150,
			StartingChips = 200,
			CurrentBet = 0,
			TotalContributedThisHand = 0,
			JoinedAt = now,
			Status = GamePlayerStatus.Active,
			RowVersion = [1, 2, 3]
		};

		var foldedPlayer = new Player
		{
			Id = Guid.NewGuid(),
			Name = "Bob",
			CreatedAt = now,
			UpdatedAt = now
		};

		var foldedGamePlayer = new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = gameId,
			Game = game,
			PlayerId = foldedPlayer.Id,
			Player = foldedPlayer,
			SeatPosition = 1,
			ChipStack = 0,
			StartingChips = 200,
			CurrentBet = 0,
			TotalContributedThisHand = 0,
			HasFolded = true,
			JoinedAt = now,
			Status = GamePlayerStatus.Active,
			RowVersion = [4, 5, 6]
		};

		var bettingRound = new BettingRound
		{
			Id = Guid.NewGuid(),
			GameId = gameId,
			Game = game,
			HandNumber = 1,
			RoundNumber = 1,
			Street = nameof(Phases.FirstBettingRound),
			CurrentBet = 0,
			MinBet = 10,
			LastRaiseAmount = 10,
			CurrentActorIndex = 0,
			PlayersInHand = 2,
			StartedAt = now
		};

		context.Games.Add(game);
		context.Players.AddRange(player, foldedPlayer);
		context.GamePlayers.AddRange(gamePlayer, foldedGamePlayer);
		context.BettingRounds.Add(bettingRound);

		for (var cardIndex = 0; cardIndex < 5; cardIndex++)
		{
			context.GameCards.Add(CreateHoleCard(game, gamePlayer, cardIndex, (CardSymbol)(cardIndex + 2), now));
			context.GameCards.Add(CreateHoleCard(game, foldedGamePlayer, cardIndex, (CardSymbol)(cardIndex + 7), now));
		}

		await context.SaveChangesAsync();
		return gameId;
	}

	private static GameCard CreateHoleCard(Game game, GamePlayer player, int cardIndex, CardSymbol symbol, DateTimeOffset now)
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
}