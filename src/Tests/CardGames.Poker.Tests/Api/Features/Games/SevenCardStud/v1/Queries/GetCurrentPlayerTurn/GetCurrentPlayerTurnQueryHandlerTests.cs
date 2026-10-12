using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using System.Security.Claims;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentPlayerTurn;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;

namespace CardGames.Poker.Tests.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentPlayerTurn;

public class GetCurrentPlayerTurnQueryHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsNull()
	{
		await using var context = CreateContext(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot());
		var sut = new GetCurrentPlayerTurnQueryHandler(context, new FakeGamePlayerResolver(null));

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(Guid.NewGuid()), CancellationToken.None);

		response.Should().BeNull();
	}

	[Fact]
	public async Task Handle_WhenCurrentPlayerSeatIsMissing_ReturnsNull()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = await SeedQueryGameAsync(databaseName, databaseRoot, 4);

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new GetCurrentPlayerTurnQueryHandler(context, new FakeGamePlayerResolver(null));

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(gameId), CancellationToken.None);

		response.Should().BeNull();
	}

	[Fact]
	public async Task Handle_ConcurrentRequests_ReturnConsistentResults()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = await SeedQueryGameAsync(databaseName, databaseRoot);
		var query = new GetCurrentPlayerTurnQuery(gameId);

		var results = await Task.WhenAll(Enumerable.Range(0, 20)
			.Select(async _ =>
			{
				await using var context = CreateContext(databaseName, databaseRoot);
				var sut = new GetCurrentPlayerTurnQueryHandler(context, new FakeGamePlayerResolver(null));
				return await sut.Handle(query, CancellationToken.None);
			}));

		results.Should().OnlyContain(result => result != null);
		results.Should().AllSatisfy(result => result.Should().BeEquivalentTo(results[0]));
		results[0]!.Player.PlayerName.Should().Be("Alice");
		results[0]!.AvailableActions.Should().NotBeNull();
		results[0]!.AvailableActions!.CanCheck.Should().BeTrue();
		results[0]!.Player.Hand.Should().BeEmpty();
		results[0]!.HandOdds.Should().BeNull();
	}

	[Fact]
	public async Task Handle_WhenCallerIsActor_ReturnsOwnHiddenCardsAndOdds()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var seeded = await SeedVisibilityGameAsync(databaseName, databaseRoot);

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new GetCurrentPlayerTurnQueryHandler(context, new FakeGamePlayerResolver(seeded.Alice));

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(seeded.GameId), CancellationToken.None);

		response.Should().NotBeNull();
		response!.Player.Hand.Select(card => card.Symbol).Should().BeEquivalentTo([CardSymbol.King, CardSymbol.Ten]);
		response.HandOdds.Should().NotBeNull();
	}

	[Fact]
	public async Task Handle_WhenCallerIsNotActor_OmitsHiddenCardsAndOdds()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var seeded = await SeedVisibilityGameAsync(databaseName, databaseRoot);

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new GetCurrentPlayerTurnQueryHandler(context, new FakeGamePlayerResolver(seeded.Bob));

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(seeded.GameId), CancellationToken.None);

		response.Should().NotBeNull();
		response!.Player.Hand.Select(card => card.Symbol).Should().Equal(CardSymbol.Ten);
		response.Player.Hand.Should().NotContain(card => card.Symbol == CardSymbol.King);
		response.Player.Hand.Should().NotContain(card => card.Symbol == CardSymbol.Ace);
		response.HandOdds.Should().BeNull();
	}

	[Fact]
	public async Task Handle_FourthStreetWithNoBet_OffersOnlyTheStreetLimit()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = await SeedQueryGameAsync(
			databaseName,
			databaseRoot,
			chipStack: 1000,
			currentBet: 0,
			minBet: 10,
			lastRaiseAmount: 0,
			phase: nameof(Phases.FourthStreet));

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new GetCurrentPlayerTurnQueryHandler(context, new FakeGamePlayerResolver(null));

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(gameId), CancellationToken.None);

		response!.AvailableActions.Should().NotBeNull();
		response.AvailableActions!.CanBet.Should().BeTrue();
		response.AvailableActions.CanAllIn.Should().BeFalse();
		response.AvailableActions.MinBet.Should().Be(10);
		response.AvailableActions.MaxBet.Should().Be(10);
	}

	[Fact]
	public async Task Handle_ThirdStreetBringIn_OffersOnlyTheComplete()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		var gameId = await SeedQueryGameAsync(
			databaseName,
			databaseRoot,
			chipStack: 1000,
			currentBet: 5,
			minBet: 10,
			lastRaiseAmount: 0);

		await using var context = CreateContext(databaseName, databaseRoot);
		var sut = new GetCurrentPlayerTurnQueryHandler(context, new FakeGamePlayerResolver(null));

		var response = await sut.Handle(new GetCurrentPlayerTurnQuery(gameId), CancellationToken.None);

		response!.AvailableActions.Should().NotBeNull();
		response.AvailableActions!.MinRaise.Should().Be(10);
		response.AvailableActions.MaxBet.Should().Be(10);
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static async Task<Guid> SeedQueryGameAsync(
		string databaseName,
		InMemoryDatabaseRoot databaseRoot,
		int currentPlayerIndex = 0,
		int chipStack = 150,
		int currentBet = 0,
		int minBet = 10,
		int lastRaiseAmount = 10,
		string phase = nameof(Phases.ThirdStreet))
	{
		var now = DateTimeOffset.UtcNow;
		var gameId = Guid.NewGuid();
		var playerId = Guid.NewGuid();
		var gamePlayerId = Guid.NewGuid();

		await using var context = CreateContext(databaseName, databaseRoot);

		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = "SEVENCARDSTUD",
			Name = "Seven Card Stud",
			BettingStructure = BettingStructure.Ante,
			MinPlayers = 2,
			MaxPlayers = 8,
			InitialHoleCards = 3,
			InitialBoardCards = 0,
			MaxCommunityCards = 0,
			MaxPlayerCards = 7,
			HasDrawPhase = false,
			MaxDiscards = 0,
			CreatedAt = now,
			UpdatedAt = now
		};

		var game = new Game
		{
			Id = gameId,
			GameTypeId = gameType.Id,
			GameType = gameType,
			CurrentPhase = phase,
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
			ChipStack = chipStack,
			StartingChips = chipStack,
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

		var bettingRound = new BettingRoundEntity
		{
			Id = Guid.NewGuid(),
			GameId = gameId,
			Game = game,
			HandNumber = 1,
			RoundNumber = 1,
			Street = phase,
			CurrentBet = currentBet,
			MinBet = minBet,
			LastRaiseAmount = lastRaiseAmount,
			CurrentActorIndex = 0,
			PlayersInHand = 2,
			StartedAt = now
		};

		context.GameTypes.Add(gameType);
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

	private static GameCard CreateHoleCard(Game game, GamePlayer player, int cardIndex, CardSymbol symbol,
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
			DealtAtPhase = nameof(Phases.ThirdStreet)
		};
	}

	private static async Task<(Guid GameId, GamePlayer Alice, GamePlayer Bob)> SeedVisibilityGameAsync(
		string databaseName,
		InMemoryDatabaseRoot databaseRoot)
	{
		var now = DateTimeOffset.UtcNow;
		var gameId = Guid.NewGuid();

		await using var context = CreateContext(databaseName, databaseRoot);

		var game = new Game
		{
			Id = gameId,
			CurrentPhase = nameof(Phases.ThirdStreet),
			CurrentHandNumber = 1,
			CurrentPlayerIndex = 0,
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

		var alice = new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = gameId,
			Game = game,
			PlayerId = alicePlayer.Id,
			Player = alicePlayer,
			SeatPosition = 0,
			ChipStack = 150,
			StartingChips = 200,
			JoinedAt = now,
			Status = GamePlayerStatus.Active,
			RowVersion = [1, 2, 3]
		};

		var bobPlayer = new Player
		{
			Id = Guid.NewGuid(),
			Name = "Bob",
			CreatedAt = now,
			UpdatedAt = now
		};

		var bob = new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = gameId,
			Game = game,
			PlayerId = bobPlayer.Id,
			Player = bobPlayer,
			SeatPosition = 1,
			ChipStack = 0,
			StartingChips = 200,
			HasFolded = true,
			JoinedAt = now,
			Status = GamePlayerStatus.Active,
			RowVersion = [4, 5, 6]
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
				HandNumber = game.CurrentHandNumber,
				DealOrder = 1,
				Location = CardLocation.Hole,
				IsVisible = false,
				Symbol = CardSymbol.King,
				Suit = CardSuit.Spades,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			},
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = gameId,
				Game = game,
				GamePlayerId = alice.Id,
				GamePlayer = alice,
				HandNumber = game.CurrentHandNumber,
				DealOrder = 2,
				Location = CardLocation.Board,
				IsVisible = true,
				Symbol = CardSymbol.Ten,
				Suit = CardSuit.Hearts,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			},
			new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = gameId,
				Game = game,
				GamePlayerId = bob.Id,
				GamePlayer = bob,
				HandNumber = game.CurrentHandNumber,
				DealOrder = 1,
				Location = CardLocation.Hole,
				IsVisible = false,
				Symbol = CardSymbol.Ace,
				Suit = CardSuit.Clubs,
				DealtAt = now,
				DealtAtPhase = nameof(Phases.ThirdStreet)
			});

		await context.SaveChangesAsync();
		return (gameId, alice, bob);
	}

	private sealed class FakeGamePlayerResolver(GamePlayer? seated) : IGamePlayerResolver
	{
		public Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, CancellationToken cancellationToken)
			=> Task.FromResult(seated);

		public Task<GamePlayer?> ResolveSeatedPlayerAsync(Guid gameId, ClaimsPrincipal user,
			CancellationToken cancellationToken)
			=> Task.FromResult(seated);

		public Task<bool> IsTableHostAsync(Guid gameId, CancellationToken cancellationToken)
			=> Task.FromResult(false);
	}
}