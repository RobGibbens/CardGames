using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGamePlayers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.Common.v1.Queries.GetGamePlayers;

public class GetGamePlayersQueryHandlerTests
{
	[Fact]
	public async Task Handle_ReturnsOnlyFaceUpCardsFromTheCurrentHand()
	{
		await using var context = CreateContext();
		var gameId = await SeedGameAsync(context);
		var sut = new GetGamePlayersQueryHandler(context, new PassThroughHybridCache());

		var response = await sut.Handle(new GetGamePlayersQuery(gameId), CancellationToken.None);

		response.Select(player => player.PlayerName).Should().BeEquivalentTo("Alice", "Bob");
		response.Should().AllSatisfy(player =>
			player.Hand.Select(card => card.Symbol).Should().ContainSingle().Which.Should().BeOneOf(CardSymbol.Ten, CardSymbol.Seven));
		response.SelectMany(player => player.Hand).Select(card => card.Symbol)
			.Should().BeEquivalentTo([CardSymbol.Ten, CardSymbol.Seven]);

		var json = JsonSerializer.Serialize(response, JsonOptions);
		json.Should().NotContain(nameof(CardSymbol.King));
		json.Should().NotContain(nameof(CardSymbol.Ace));
		json.Should().NotContain(nameof(CardSymbol.Queen));
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new CardsDbContext(options);
	}

	private sealed class PassThroughHybridCache : HybridCache
	{
		public override ValueTask<T> GetOrCreateAsync<TState, T>(
			string key,
			TState state,
			Func<TState, CancellationToken, ValueTask<T>> factory,
			HybridCacheEntryOptions? options = null,
			IEnumerable<string>? tags = null,
			CancellationToken cancellationToken = default)
			=> factory(state, cancellationToken);

		public override ValueTask SetAsync<T>(
			string key,
			T value,
			HybridCacheEntryOptions? options = null,
			IEnumerable<string>? tags = null,
			CancellationToken cancellationToken = default)
			=> ValueTask.CompletedTask;

		public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
			=> ValueTask.CompletedTask;

		public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
			=> ValueTask.CompletedTask;
	}

	private static async Task<Guid> SeedGameAsync(CardsDbContext context)
	{
		var now = DateTimeOffset.UtcNow;
		var gameId = Guid.NewGuid();
		var game = new Game
		{
			Id = gameId,
			CurrentPhase = "ThirdStreet",
			CurrentHandNumber = 1,
			CreatedAt = now,
			UpdatedAt = now
		};

		var alice = SeatedPlayer(game, "Alice", 0, now);
		var bob = SeatedPlayer(game, "Bob", 1, now);

		context.Games.Add(game);
		context.GamePlayers.AddRange(alice, bob);
		context.GameCards.AddRange(
			Card(game, alice, CardSymbol.King, CardSuit.Spades, handNumber: 1, dealOrder: 1, visible: false, now),
			Card(game, alice, CardSymbol.Ten, CardSuit.Hearts, handNumber: 1, dealOrder: 2, visible: true, now),
			Card(game, alice, CardSymbol.Queen, CardSuit.Diamonds, handNumber: 0, dealOrder: 1, visible: false, now),
			Card(game, bob, CardSymbol.Ace, CardSuit.Clubs, handNumber: 1, dealOrder: 1, visible: false, now),
			Card(game, bob, CardSymbol.Seven, CardSuit.Diamonds, handNumber: 1, dealOrder: 2, visible: true, now));
		await context.SaveChangesAsync();
		return gameId;
	}

	private static GamePlayer SeatedPlayer(Game game, string name, int seat, DateTimeOffset now)
	{
		var player = new Player
		{
			Id = Guid.NewGuid(),
			Name = name,
			CreatedAt = now,
			UpdatedAt = now
		};

		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seat,
			ChipStack = 100,
			StartingChips = 100,
			JoinedAt = now,
			Status = GamePlayerStatus.Active,
			RowVersion = [1]
		};
	}

	private static GameCard Card(
		Game game,
		GamePlayer player,
		CardSymbol symbol,
		CardSuit suit,
		int handNumber,
		int dealOrder,
		bool visible,
		DateTimeOffset now)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = handNumber,
			DealOrder = dealOrder,
			Location = visible ? CardLocation.Board : CardLocation.Hole,
			IsVisible = visible,
			Symbol = symbol,
			Suit = suit,
			DealtAt = now,
			DealtAtPhase = "ThirdStreet"
		};
	}

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() }
	};
}
