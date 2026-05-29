#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

public class GetCurrentPlayerTurnQueryHandlerTests
{
	[Fact]
	public async Task Handle_ConcurrentRequests_ReturnConsistentResults()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = Guid.NewGuid();
		var playerId = Guid.NewGuid();
		var gamePlayerId = Guid.NewGuid();

		await using (var seedContext = CreateContext(databaseName, databaseRoot))
		{
			var game = new Game
			{
				Id = gameId,
				CurrentPhase = "ThirdStreet",
				CurrentHandNumber = 1,
				CurrentPlayerIndex = 0,
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
			};

			var player = new Player
			{
				Id = playerId,
				Name = "Lynne",
				CreatedAt = DateTimeOffset.UtcNow,
				UpdatedAt = DateTimeOffset.UtcNow
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
				CurrentBet = 10,
				TotalContributedThisHand = 10,
				JoinedAt = DateTimeOffset.UtcNow,
				Status = GamePlayerStatus.Active,
				RowVersion = [1, 2, 3]
			};

			var bettingRound = new BettingRound
			{
				Id = Guid.NewGuid(),
				GameId = gameId,
				Game = game,
				HandNumber = 1,
				RoundNumber = 1,
				Street = "ThirdStreet",
				CurrentBet = 10,
				MinBet = 5,
				LastRaiseAmount = 5,
				CurrentActorIndex = 0,
				PlayersInHand = 1,
				StartedAt = DateTimeOffset.UtcNow
			};

			var visibleCard = new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = gameId,
				Game = game,
				GamePlayerId = gamePlayerId,
				GamePlayer = gamePlayer,
				HandNumber = 1,
				DealOrder = 1,
				Location = CardLocation.Board,
				IsVisible = true,
				Symbol = CardSymbol.Ace,
				Suit = CardSuit.Spades,
				DealtAt = DateTimeOffset.UtcNow,
				DealtAtPhase = "ThirdStreet"
			};

			var holeCard = new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = gameId,
				Game = game,
				GamePlayerId = gamePlayerId,
				GamePlayer = gamePlayer,
				HandNumber = 1,
				DealOrder = 2,
				Location = CardLocation.Hole,
				IsVisible = false,
				Symbol = CardSymbol.Four,
				Suit = CardSuit.Hearts,
				DealtAt = DateTimeOffset.UtcNow,
				DealtAtPhase = "ThirdStreet"
			};

			seedContext.Games.Add(game);
			seedContext.Players.Add(player);
			seedContext.GamePlayers.Add(gamePlayer);
			seedContext.BettingRounds.Add(bettingRound);
			seedContext.GameCards.AddRange(visibleCard, holeCard);
			await seedContext.SaveChangesAsync();
		}

		using var serviceProvider = CreateServiceProvider();
		var query = new GetCurrentPlayerTurnQuery(gameId);

		var results = await Task.WhenAll(Enumerable.Range(0, 20)
			.Select(async _ =>
			{
				await using var context = CreateContext(databaseName, databaseRoot);
				var sut = new GetCurrentPlayerTurnQueryHandler(context, serviceProvider.GetRequiredService<HybridCache>());
				return await sut.Handle(query, CancellationToken.None);
			}));

		results.Should().OnlyContain(result => result != null);
		results.Should().AllSatisfy(result => result.Should().BeEquivalentTo(results[0]));
		results[0]!.Player.PlayerName.Should().Be("Lynne");
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
}