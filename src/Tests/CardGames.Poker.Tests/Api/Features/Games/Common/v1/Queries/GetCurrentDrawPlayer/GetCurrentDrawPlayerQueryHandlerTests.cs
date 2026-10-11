using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetCurrentDrawPlayer;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.Common.v1.Queries.GetCurrentDrawPlayer;

public class GetCurrentDrawPlayerQueryHandlerTests
{
	[Fact]
	public async Task Handle_ReturnsTheDrawingPlayerWithoutCards()
	{
		await using var context = CreateContext();
		var gameId = await SeedGameAsync(context);
		await using var serviceProvider = CreateServiceProvider();
		var sut = new GetCurrentDrawPlayerQueryHandler(context, serviceProvider.GetRequiredService<HybridCache>());

		var response = await sut.Handle(new GetCurrentDrawPlayerQuery(gameId), CancellationToken.None);

		response.Should().NotBeNull();
		response!.PlayerName.Should().Be("Alice");
		response.SeatPosition.Should().Be(0);
		response.Hand.Should().BeEmpty();

		var json = JsonSerializer.Serialize(response, JsonOptions);
		json.Should().NotContain(nameof(CardSymbol.King));
		json.Should().NotContain(nameof(CardSymbol.Ace));
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
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

	private static async Task<Guid> SeedGameAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var gameId = Guid.NewGuid();
		var game = new Game
		{
			Id = gameId,
			CurrentPhase = "DrawPhase",
			CurrentHandNumber = 1,
			CurrentDrawPlayerIndex = 0,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alicePlayer = new Player
		{
			Id = Guid.NewGuid(),
			Name = "Alice",
			CreatedAt = now,
			UpdatedAt = now
		};
		var bobPlayer = new Player
		{
			Id = Guid.NewGuid(),
			Name = "Bob",
			CreatedAt = now,
			UpdatedAt = now
		};
		var alice = new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = gameId,
			Game = game,
			PlayerId = alicePlayer.Id,
			Player = alicePlayer,
			SeatPosition = 0,
			ChipStack = 100,
			StartingChips = 100,
			JoinedAt = now,
			Status = GamePlayerStatus.Active,
			RowVersion = [1]
		};
		var bob = new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = gameId,
			Game = game,
			PlayerId = bobPlayer.Id,
			Player = bobPlayer,
			SeatPosition = 1,
			ChipStack = 100,
			StartingChips = 100,
			JoinedAt = now,
			Status = GamePlayerStatus.Active,
			RowVersion = [2]
		};

		context.Games.Add(game);
		context.Players.AddRange(alicePlayer, bobPlayer);
		context.GamePlayers.AddRange(alice, bob);
		context.GameCards.AddRange(
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = gameId,
				Game = game,
				GamePlayerId = alice.Id,
				GamePlayer = alice,
				HandNumber = 1,
				DealOrder = 1,
				Location = CardLocation.Hole,
				IsVisible = false,
				Symbol = CardSymbol.King,
				Suit = CardSuit.Spades,
				DealtAt = now,
				DealtAtPhase = "DrawPhase"
			},
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = gameId,
				Game = game,
				GamePlayerId = bob.Id,
				GamePlayer = bob,
				HandNumber = 1,
				DealOrder = 1,
				Location = CardLocation.Hole,
				IsVisible = false,
				Symbol = CardSymbol.Ace,
				Suit = CardSuit.Clubs,
				DealtAt = now,
				DealtAtPhase = "DrawPhase"
			});
		await context.SaveChangesAsync();
		return gameId;
	}

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};
}
