#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;
using CardGames.Poker.Api.Games;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;
using BettingStructure = CardGames.Poker.Betting.BettingStructure;
using Phases = CardGames.Poker.Betting.Phases;
using BettingRoundEntity = CardGames.Poker.Api.Data.Entities.BettingRound;

namespace CardGames.Poker.Tests.Api.Features.Games.Tollbooth.v1.Commands.ChooseCard;

public class ChooseCardCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(Guid.NewGuid(), TollboothChoice.Furthest), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotInTollboothOffer_ReturnsNotInTollboothPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, currentPhase: nameof(Phases.ThirdStreet));
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(game.Id, TollboothChoice.Furthest), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.NotInTollboothPhase);
		result.AsT1.Message.Should().Contain(nameof(Phases.ThirdStreet));
	}

	[Fact]
	public async Task Handle_WhenGameTypeIsIncorrect_ReturnsNotInTollboothPhase()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, gameTypeCode: PokerGameMetadataRegistry.SevenCardStudCode);
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(game.Id, TollboothChoice.Furthest), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.NotInTollboothPhase);
		result.AsT1.Message.Should().Contain(PokerGameMetadataRegistry.SevenCardStudCode);
	}

	[Fact]
	public async Task Handle_WhenNoEligiblePlayersRemain_ReturnsNoEligiblePlayers()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			hasDrawnBySeat: new Dictionary<int, bool>
			{
				[0] = true,
				[1] = true
			});
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(game.Id, TollboothChoice.Furthest), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.NoEligiblePlayers);
	}

	[Fact]
	public async Task Handle_WhenRequestedSeatIsMissing_ReturnsNotPlayerTurn()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context);
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, PlayerSeatIndex: 99), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.NotPlayerTurn);
	}

	[Fact]
	public async Task Handle_WhenPlayerAlreadyChoseThisRound_ReturnsAlreadyChosen()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			hasDrawnBySeat: new Dictionary<int, bool>
			{
				[0] = true,
				[1] = false
			});
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(game.Id, TollboothChoice.Furthest, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.AlreadyChosen);
	}

	[Fact]
	public async Task Handle_WhenPlayerCannotAffordChoice_ReturnsCannotAfford()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(
			context,
			chipStacksBySeat: new Dictionary<int, int>
			{
				[0] = 4,
				[1] = 100
			});
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(game.Id, TollboothChoice.Nearest, PlayerSeatIndex: 0), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.CannotAfford);
		result.AsT1.Message.Should().Contain("costs 5");
	}

	[Fact]
	public async Task Handle_WhenDisplayCardsAreMissing_ReturnsInvalidChoice()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, communityCardCount: 0);
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(game.Id, TollboothChoice.Furthest), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.InvalidChoice);
		result.AsT1.Message.Should().Contain("No display cards available");
	}

	[Fact]
	public async Task Handle_WhenDeckCardsAreMissing_ReturnsInvalidChoice()
	{
		await using var context = CreateContext();
		var game = await SeedGameAsync(context, deckCardCount: 0);
		var sut = new ChooseCardCommandHandler(context);

		var result = await sut.Handle(new ChooseCardCommand(game.Id, TollboothChoice.Deck), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ChooseCardErrorCode.InvalidChoice);
		result.AsT1.Message.Should().Contain("No deck cards available");
	}

	[Fact]
	public async Task Handle_WhenSecondChoiceStartsFromStaleSnapshot_CompletesOfferRound()
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
			.Include(game => game.GamePlayers)
				.ThenInclude(gamePlayer => gamePlayer.Player)
			.Include(game => game.GameType)
			.Include(game => game.Pots)
			.FirstAsync(game => game.Id == gameId);

		var freshSut = new ChooseCardCommandHandler(freshContext);
		var staleSut = new ChooseCardCommandHandler(staleContext);

		var firstResult = await freshSut.Handle(
			new ChooseCardCommand(gameId, TollboothChoice.Furthest, PlayerSeatIndex: 0),
			CancellationToken.None);
		var secondResult = await staleSut.Handle(
			new ChooseCardCommand(gameId, TollboothChoice.Furthest, PlayerSeatIndex: 1),
			CancellationToken.None);

		firstResult.IsT0.Should().BeTrue();
		secondResult.IsT0.Should().BeTrue();

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedGame = await verificationContext.Games.FirstAsync(game => game.Id == gameId);
		var players = await verificationContext.GamePlayers
			.Where(gamePlayer => gamePlayer.GameId == gameId)
			.OrderBy(gamePlayer => gamePlayer.SeatPosition)
			.ToListAsync();
		var playerCards = await verificationContext.GameCards
			.Where(gameCard => gameCard.GameId == gameId &&
			                  gameCard.HandNumber == persistedGame.CurrentHandNumber &&
			                  gameCard.GamePlayerId != null &&
			                  gameCard.Location != CardLocation.Deck)
			.ToListAsync();

		persistedGame.CurrentPhase.Should().Be(nameof(Phases.FourthStreet));
		persistedGame.CurrentDrawPlayerIndex.Should().Be(-1);
		players.Should().OnlyContain(player => !player.HasDrawnThisRound);
		playerCards.Should().Contain(card => card.GamePlayerId == players[0].Id && card.DealOrder == 4);
		playerCards.Should().Contain(card => card.GamePlayerId == players[1].Id && card.DealOrder == 4);
		verificationContext.BettingRounds.Should().ContainSingle(round => round.GameId == gameId && round.Street == nameof(Phases.FourthStreet));
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
		string currentPhase = nameof(Phases.TollboothOffer),
		string gameTypeCode = PokerGameMetadataRegistry.TollboothCode,
		IReadOnlyDictionary<int, bool>? hasDrawnBySeat = null,
		IReadOnlyDictionary<int, int>? chipStacksBySeat = null,
		int currentDrawPlayerIndex = 0,
		int currentPlayerIndex = 0,
		int dealerPosition = 1,
		int communityCardCount = 2,
		int deckCardCount = 3)
	{
		var now = DateTimeOffset.UtcNow;
		hasDrawnBySeat ??= new Dictionary<int, bool>();
		chipStacksBySeat ??= new Dictionary<int, int>();

		var gameType = new GameType
		{
			Id = Guid.NewGuid(),
			Code = gameTypeCode,
			Name = gameTypeCode,
			BettingStructure = BettingStructure.Ante,
			MinPlayers = 2,
			MaxPlayers = 8,
			InitialHoleCards = 3,
			InitialBoardCards = 0,
			MaxCommunityCards = 2,
			MaxPlayerCards = 7,
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
			CurrentHandGameTypeCode = gameTypeCode,
			CurrentPhase = currentPhase,
			CurrentHandNumber = 1,
			CurrentDrawPlayerIndex = currentDrawPlayerIndex,
			CurrentPlayerIndex = currentPlayerIndex,
			DealerPosition = dealerPosition,
			Ante = 5,
			SmallBet = 10,
			BigBet = 20,
			MinBet = 5,
			Status = GameStatus.InProgress,
			GameSettings = "{\"PreviousBettingStreet\":\"ThirdStreet\"}",
			CreatedAt = now,
			UpdatedAt = now
		};

		var players = new[]
		{
			CreatePlayer("Alice", now),
			CreatePlayer("Bob", now)
		};

		var gamePlayers = players
			.Select((player, seatPosition) => CreateGamePlayer(
				game,
				player,
				seatPosition,
				hasDrawnBySeat.TryGetValue(seatPosition, out var hasDrawn) && hasDrawn,
				chipStacksBySeat.TryGetValue(seatPosition, out var chipStack) ? chipStack : 100,
				now))
			.ToArray();

		context.GameTypes.Add(gameType);
		context.Games.Add(game);
		context.Players.AddRange(players);
		context.GamePlayers.AddRange(gamePlayers);
		context.Pots.Add(new Pot
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 0,
			CreatedAt = now
		});

		for (var seatPosition = 0; seatPosition < gamePlayers.Length; seatPosition++)
		{
			SeedStudCards(context, game, gamePlayers[seatPosition], seatPosition, now);
		}

		for (var cardIndex = 0; cardIndex < communityCardCount; cardIndex++)
		{
			context.GameCards.Add(new GameCard
			{
				Id = Guid.NewGuid(),
				GameId = game.Id,
				Game = game,
				GamePlayerId = null,
				HandNumber = game.CurrentHandNumber,
				DealOrder = cardIndex + 1,
				Location = CardLocation.Community,
				IsVisible = true,
				Symbol = (CardSymbol)(cardIndex + 11),
				Suit = (CardSuit)(cardIndex % 4),
				DealtAt = now,
				DealtAtPhase = nameof(Phases.TollboothOffer)
			});
		}

		for (var cardIndex = 0; cardIndex < deckCardCount; cardIndex++)
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
				Symbol = (CardSymbol)(cardIndex + 4),
				Suit = (CardSuit)((cardIndex + 1) % 4),
				DealtAt = now,
				DealtAtPhase = nameof(Phases.Dealing)
			});
		}

		context.BettingRounds.Add(new BettingRoundEntity
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			HandNumber = game.CurrentHandNumber,
			RoundNumber = 1,
			Street = nameof(Phases.ThirdStreet),
			CurrentBet = 0,
			MinBet = 10,
			CurrentActorIndex = currentPlayerIndex,
			PlayersInHand = gamePlayers.Length,
			StartedAt = now,
			IsComplete = true
		});

		await context.SaveChangesAsync();
		return game;
	}

	private static void SeedStudCards(CardsDbContext context, Game game, GamePlayer player, int seatPosition, DateTimeOffset now)
	{
		context.GameCards.Add(new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = 1,
			Location = CardLocation.Hole,
			IsVisible = false,
			Symbol = (CardSymbol)(seatPosition + 2),
			Suit = CardSuit.Hearts,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.ThirdStreet)
		});

		context.GameCards.Add(new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = 2,
			Location = CardLocation.Hole,
			IsVisible = false,
			Symbol = (CardSymbol)(seatPosition + 6),
			Suit = CardSuit.Spades,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.ThirdStreet)
		});

		context.GameCards.Add(new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			GamePlayerId = player.Id,
			GamePlayer = player,
			HandNumber = game.CurrentHandNumber,
			DealOrder = 3,
			Location = CardLocation.Board,
			IsVisible = true,
			Symbol = (CardSymbol)(seatPosition + 9),
			Suit = CardSuit.Clubs,
			DealtAt = now,
			DealtAtPhase = nameof(Phases.ThirdStreet)
		});
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

	private static GamePlayer CreateGamePlayer(
		Game game,
		Player player,
		int seatPosition,
		bool hasDrawnThisRound,
		int chipStack,
		DateTimeOffset now)
	{
		return new GamePlayer
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			Game = game,
			PlayerId = player.Id,
			Player = player,
			SeatPosition = seatPosition,
			ChipStack = chipStack,
			StartingChips = 100,
			CurrentBet = 0,
			TotalContributedThisHand = 0,
			HasFolded = false,
			IsAllIn = false,
			HasDrawnThisRound = hasDrawnThisRound,
			Status = GamePlayerStatus.Active,
			JoinedAt = now,
			RowVersion = [1, 2, 3]
		};
	}
}