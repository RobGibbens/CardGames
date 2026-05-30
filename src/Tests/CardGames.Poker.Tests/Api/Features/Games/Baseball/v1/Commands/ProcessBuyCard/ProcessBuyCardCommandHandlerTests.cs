#nullable enable

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Baseball;
using CardGames.Poker.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;
using CardGames.Poker.Betting;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NSubstitute;
using Xunit;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Tests.Api.Features.Games.Baseball.v1.Commands.ProcessBuyCard;

public class ProcessBuyCardCommandHandlerTests
{
	[Fact]
	public async Task Handle_WhenGameIsMissing_ReturnsGameNotFound()
	{
		await using var context = CreateContext();
		var sut = new ProcessBuyCardCommandHandler(context, Substitute.For<IMediator>());

		var result = await sut.Handle(new ProcessBuyCardCommand(Guid.NewGuid(), Guid.NewGuid(), Accept: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBuyCardErrorCode.GameNotFound);
	}

	[Fact]
	public async Task Handle_WhenPlayerIsMissingFromGame_ReturnsPlayerNotFound()
	{
		await using var context = CreateContext();
		var game = CreateGame(currentPhase: nameof(Phases.BuyCardOffer));
		context.Games.Add(game);
		await context.SaveChangesAsync();

		var sut = new ProcessBuyCardCommandHandler(context, Substitute.For<IMediator>());

		var result = await sut.Handle(new ProcessBuyCardCommand(game.Id, Guid.NewGuid(), Accept: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBuyCardErrorCode.PlayerNotFound);
	}

	[Fact]
	public async Task Handle_WhenGameIsNotAwaitingBuyCardDecision_ReturnsInvalidGameState()
	{
		await using var context = CreateContext();
		var game = CreateGame(currentPhase: nameof(Phases.ThirdStreet));
		var player = CreatePlayer();
		var gamePlayer = CreateGamePlayer(game, player, seatPosition: 0, chipStack: 100);
		context.Games.Add(game);
		context.Players.Add(player);
		context.GamePlayers.Add(gamePlayer);
		await context.SaveChangesAsync();

		var sut = new ProcessBuyCardCommandHandler(context, Substitute.For<IMediator>());

		var result = await sut.Handle(new ProcessBuyCardCommand(game.Id, player.Id, Accept: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBuyCardErrorCode.InvalidGameState);
	}

	[Fact]
	public async Task Handle_WhenSettingsPayloadIsMalformed_ReturnsNoPendingOffer()
	{
		await using var context = CreateContext();
		var game = CreateGame(currentPhase: nameof(Phases.BuyCardOffer));
		game.GameSettings = "{ malformed json";
		var player = CreatePlayer();
		var gamePlayer = CreateGamePlayer(game, player, seatPosition: 0, chipStack: 100);
		context.Games.Add(game);
		context.Players.Add(player);
		context.GamePlayers.Add(gamePlayer);
		await context.SaveChangesAsync();

		var sut = new ProcessBuyCardCommandHandler(context, Substitute.For<IMediator>());

		var result = await sut.Handle(new ProcessBuyCardCommand(game.Id, player.Id, Accept: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBuyCardErrorCode.NoPendingOffer);
	}

	[Fact]
	public async Task Handle_WhenNoPendingOffersExist_ReturnsNoPendingOffer()
	{
		await using var context = CreateContext();
		var game = CreateGame(currentPhase: nameof(Phases.BuyCardOffer));
		var player = CreatePlayer();
		var gamePlayer = CreateGamePlayer(game, player, seatPosition: 0, chipStack: 100);

		context.Games.Add(game);
		context.Players.Add(player);
		context.GamePlayers.Add(gamePlayer);
		await context.SaveChangesAsync();

		var sut = new ProcessBuyCardCommandHandler(context, Substitute.For<IMediator>());

		var result = await sut.Handle(new ProcessBuyCardCommand(game.Id, player.Id, Accept: false), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBuyCardErrorCode.NoPendingOffer);
	}

	[Fact]
	public async Task Handle_WhenPendingOfferBelongsToDifferentPlayer_ReturnsNoPendingOffer()
	{
		await using var context = CreateContext();
		var game = CreateGame(currentPhase: nameof(Phases.BuyCardOffer), minBet: 25);
		var player = CreatePlayer("Rob");
		var otherPlayer = CreatePlayer("Sam");
		var gamePlayer = CreateGamePlayer(game, player, seatPosition: 0, chipStack: 100);
		var otherGamePlayer = CreateGamePlayer(game, otherPlayer, seatPosition: 1, chipStack: 100);

		BaseballGameSettings.SaveState(game, new BaseballGameSettings.BuyCardState(
			BuyCardPrice: 25,
			PendingOffers:
			[
				new BaseballGameSettings.BuyCardOfferState(otherPlayer.Id, otherGamePlayer.SeatPosition, Guid.NewGuid(), nameof(Phases.FourthStreet))
			],
			ReturnPhase: nameof(Phases.FourthStreet),
			ReturnActorIndex: otherGamePlayer.SeatPosition));

		context.Games.Add(game);
		context.Players.AddRange(player, otherPlayer);
		context.GamePlayers.AddRange(gamePlayer, otherGamePlayer);
		await context.SaveChangesAsync();

		var sut = new ProcessBuyCardCommandHandler(context, Substitute.For<IMediator>());

		var result = await sut.Handle(new ProcessBuyCardCommand(game.Id, player.Id, Accept: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBuyCardErrorCode.NoPendingOffer);
	}

	[Fact]
	public async Task Handle_WhenTriggerCardIsMissing_AppendsBoughtCardAndKeepsNextOffer()
	{
		await using var context = CreateContext();
		var game = CreateGame(currentPhase: nameof(Phases.BuyCardOffer), minBet: 10);
		var player = CreatePlayer("Rob");
		var otherPlayer = CreatePlayer("Sam");
		var gamePlayer = CreateGamePlayer(game, player, seatPosition: 0, chipStack: 100);
		var otherGamePlayer = CreateGamePlayer(game, otherPlayer, seatPosition: 1, chipStack: 100);
		var existingFirstCard = CreatePlayerCard(game, gamePlayer, dealOrder: 1, CardLocation.Hole, CardSymbol.Ace, CardSuit.Spades);
		var existingSecondCard = CreatePlayerCard(game, gamePlayer, dealOrder: 2, CardLocation.Board, CardSymbol.King, CardSuit.Hearts);
		var existingThirdCard = CreatePlayerCard(game, gamePlayer, dealOrder: 3, CardLocation.Board, CardSymbol.Queen, CardSuit.Diamonds);
		var otherTriggerCard = CreatePlayerCard(game, otherGamePlayer, dealOrder: 2, CardLocation.Board, CardSymbol.Four, CardSuit.Clubs);
		var deckCard = CreateDeckCard(game, dealOrder: 100, CardSymbol.Ten, CardSuit.Clubs);
		var pot = CreateMainPot(game);

		BaseballGameSettings.SaveState(game, new BaseballGameSettings.BuyCardState(
			BuyCardPrice: 10,
			PendingOffers:
			[
				new BaseballGameSettings.BuyCardOfferState(player.Id, gamePlayer.SeatPosition, Guid.NewGuid(), nameof(Phases.FourthStreet)),
				new BaseballGameSettings.BuyCardOfferState(otherPlayer.Id, otherGamePlayer.SeatPosition, otherTriggerCard.Id, nameof(Phases.FourthStreet))
			],
			ReturnPhase: "ResumeBuyCardRound",
			ReturnActorIndex: otherGamePlayer.SeatPosition));

		context.Games.Add(game);
		context.Players.AddRange(player, otherPlayer);
		context.GamePlayers.AddRange(gamePlayer, otherGamePlayer);
		context.GameCards.AddRange(existingFirstCard, existingSecondCard, existingThirdCard, otherTriggerCard, deckCard);
		context.Pots.Add(pot);
		await context.SaveChangesAsync();

		var sut = new ProcessBuyCardCommandHandler(context, Substitute.For<IMediator>());

		var result = await sut.Handle(new ProcessBuyCardCommand(game.Id, player.Id, Accept: true), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.CurrentPhase.Should().Be(nameof(Phases.BuyCardOffer));

		var persistedGame = await context.Games.SingleAsync(savedGame => savedGame.Id == game.Id);
		var updatedState = BaseballGameSettings.GetState(persistedGame, defaultBuyCardPrice: 0);
		var boughtCard = await context.GameCards
			.SingleAsync(gameCard => gameCard.GameId == game.Id && gameCard.GamePlayerId == gamePlayer.Id && gameCard.IsBuyCard);
		var persistedPlayer = await context.GamePlayers.SingleAsync(savedPlayer => savedPlayer.Id == gamePlayer.Id);
		var persistedPot = await context.Pots.Include(savedPot => savedPot.Contributions).SingleAsync(savedPot => savedPot.GameId == game.Id);

		updatedState.PendingOffers.Should().ContainSingle();
		updatedState.PendingOffers[0].PlayerId.Should().Be(otherPlayer.Id);
		persistedGame.CurrentPlayerIndex.Should().Be(otherGamePlayer.SeatPosition);
		boughtCard.DealOrder.Should().Be(4);
		boughtCard.Location.Should().Be(CardLocation.Board);
		boughtCard.IsVisible.Should().BeTrue();
		persistedPlayer.ChipStack.Should().Be(90);
		persistedPot.Amount.Should().Be(10);
	}

	[Fact]
	public async Task Handle_WhenDecisionStartsFromStaleSnapshot_UsesFreshOfferState()
	{
		var databaseName = Guid.NewGuid().ToString();
		var databaseRoot = new InMemoryDatabaseRoot();
		Guid gameId;
		Guid firstPlayerId;
		Guid secondPlayerId;

		await using (var setupContext = CreateContext(databaseName, databaseRoot))
		{
			var game = CreateGame(currentPhase: nameof(Phases.BuyCardOffer), minBet: 5);
			var firstPlayer = CreatePlayer("Rob");
			var secondPlayer = CreatePlayer("Sam");
			var firstGamePlayer = CreateGamePlayer(game, firstPlayer, seatPosition: 0, chipStack: 100);
			var secondGamePlayer = CreateGamePlayer(game, secondPlayer, seatPosition: 1, chipStack: 100);
			var firstTriggerCard = CreatePlayerCard(game, firstGamePlayer, dealOrder: 2, CardLocation.Board, CardSymbol.Four, CardSuit.Hearts);
			var secondTriggerCard = CreatePlayerCard(game, secondGamePlayer, dealOrder: 2, CardLocation.Board, CardSymbol.Four, CardSuit.Spades);
			var firstOtherCard = CreatePlayerCard(game, firstGamePlayer, dealOrder: 3, CardLocation.Board, CardSymbol.King, CardSuit.Clubs);
			var secondOtherCard = CreatePlayerCard(game, secondGamePlayer, dealOrder: 3, CardLocation.Board, CardSymbol.Queen, CardSuit.Diamonds);
			var firstDeckCard = CreateDeckCard(game, dealOrder: 100, CardSymbol.Ten, CardSuit.Clubs);
			var secondDeckCard = CreateDeckCard(game, dealOrder: 101, CardSymbol.Jack, CardSuit.Diamonds);
			var pot = CreateMainPot(game);

			BaseballGameSettings.SaveState(game, new BaseballGameSettings.BuyCardState(
				BuyCardPrice: 5,
				PendingOffers:
				[
					new BaseballGameSettings.BuyCardOfferState(firstPlayer.Id, firstGamePlayer.SeatPosition, firstTriggerCard.Id, nameof(Phases.FourthStreet)),
					new BaseballGameSettings.BuyCardOfferState(secondPlayer.Id, secondGamePlayer.SeatPosition, secondTriggerCard.Id, nameof(Phases.FourthStreet))
				],
				ReturnPhase: "ResumeBuyCardsComplete",
				ReturnActorIndex: 0));

			setupContext.Games.Add(game);
			setupContext.Players.AddRange(firstPlayer, secondPlayer);
			setupContext.GamePlayers.AddRange(firstGamePlayer, secondGamePlayer);
			setupContext.GameCards.AddRange(firstTriggerCard, secondTriggerCard, firstOtherCard, secondOtherCard, firstDeckCard, secondDeckCard);
			setupContext.Pots.Add(pot);
			await setupContext.SaveChangesAsync();

			gameId = game.Id;
			firstPlayerId = firstPlayer.Id;
			secondPlayerId = secondPlayer.Id;
		}

		await using var staleContext = CreateContext(databaseName, databaseRoot);
		await using var freshContext = CreateContext(databaseName, databaseRoot);

		_ = await staleContext.Games
			.Include(game => game.GamePlayers)
				.ThenInclude(gamePlayer => gamePlayer.Player)
			.Include(game => game.Pots)
				.ThenInclude(pot => pot.Contributions)
			.FirstAsync(game => game.Id == gameId);

		var freshSut = new ProcessBuyCardCommandHandler(freshContext, Substitute.For<IMediator>());
		var staleSut = new ProcessBuyCardCommandHandler(staleContext, Substitute.For<IMediator>());

		var firstResult = await freshSut.Handle(new ProcessBuyCardCommand(gameId, firstPlayerId, Accept: true), CancellationToken.None);
		var secondResult = await staleSut.Handle(new ProcessBuyCardCommand(gameId, secondPlayerId, Accept: true), CancellationToken.None);

		firstResult.IsT0.Should().BeTrue();
		secondResult.IsT0.Should().BeTrue();

		await using var verificationContext = CreateContext(databaseName, databaseRoot);
		var persistedGame = await verificationContext.Games.SingleAsync(game => game.Id == gameId);
		var updatedState = BaseballGameSettings.GetState(persistedGame, defaultBuyCardPrice: 0);
		var players = await verificationContext.GamePlayers
			.Where(gamePlayer => gamePlayer.GameId == gameId)
			.OrderBy(gamePlayer => gamePlayer.SeatPosition)
			.ToListAsync();
		var buyCards = await verificationContext.GameCards
			.Where(gameCard => gameCard.GameId == gameId && gameCard.IsBuyCard)
			.OrderBy(gameCard => gameCard.GamePlayerId)
			.ToListAsync();
		var persistedPot = await verificationContext.Pots.Include(pot => pot.Contributions).SingleAsync(pot => pot.GameId == gameId);

		updatedState.PendingOffers.Should().BeEmpty();
		persistedGame.CurrentPhase.Should().Be("ResumeBuyCardsComplete");
		players.Select(gamePlayer => gamePlayer.ChipStack).Should().Equal(95, 95);
		buyCards.Should().HaveCount(2);
		buyCards.Select(gameCard => gameCard.GamePlayerId).Should().OnlyHaveUniqueItems();
		persistedPot.Amount.Should().Be(10);
		persistedPot.Contributions.Should().HaveCount(2);
	}

	[Fact]
	public async Task Handle_WhenPlayerCannotAffordBuyCard_ReturnsInsufficientChips()
	{
		await using var context = CreateContext();
		var game = CreateGame(currentPhase: nameof(Phases.BuyCardOffer), minBet: 25);
		var player = CreatePlayer();
		var gamePlayer = CreateGamePlayer(game, player, seatPosition: 0, chipStack: 10);
		BaseballGameSettings.SaveState(game, new BaseballGameSettings.BuyCardState(
			BuyCardPrice: 25,
			PendingOffers:
			[
				new BaseballGameSettings.BuyCardOfferState(player.Id, gamePlayer.SeatPosition, Guid.NewGuid(), nameof(Phases.FourthStreet))
			],
			ReturnPhase: nameof(Phases.FourthStreet),
			ReturnActorIndex: gamePlayer.SeatPosition));

		context.Games.Add(game);
		context.Players.Add(player);
		context.GamePlayers.Add(gamePlayer);
		await context.SaveChangesAsync();

		var sut = new ProcessBuyCardCommandHandler(context, Substitute.For<IMediator>());

		var result = await sut.Handle(new ProcessBuyCardCommand(game.Id, player.Id, Accept: true), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(ProcessBuyCardErrorCode.InsufficientChips);
	}

	private static CardsDbContext CreateContext()
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString())
			.Options;

		return new CardsDbContext(options);
	}

	private static CardsDbContext CreateContext(string databaseName, InMemoryDatabaseRoot databaseRoot)
	{
		var options = new DbContextOptionsBuilder<CardsDbContext>()
			.UseInMemoryDatabase(databaseName, databaseRoot)
			.Options;

		return new CardsDbContext(options);
	}

	private static Game CreateGame(string currentPhase, int currentHandNumber = 1, int? minBet = null)
	{
		return new Game
		{
			Id = Guid.NewGuid(),
			CurrentPhase = currentPhase,
			CurrentHandNumber = currentHandNumber,
			MinBet = minBet,
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		};
	}

	private static Player CreatePlayer(string name = "Rob")
	{
		return new Player
		{
			Id = Guid.NewGuid(),
			Name = name,
			CreatedAt = DateTimeOffset.UtcNow,
			UpdatedAt = DateTimeOffset.UtcNow
		};
	}

	private static GamePlayer CreateGamePlayer(Game game, Player player, int seatPosition, int chipStack)
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
			StartingChips = chipStack,
			JoinedAt = DateTimeOffset.UtcNow,
			Status = GamePlayerStatus.Active
		};
	}

	private static GameCard CreatePlayerCard(
		Game game,
		GamePlayer gamePlayer,
		int dealOrder,
		CardLocation location,
		CardSymbol symbol,
		CardSuit suit)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			GamePlayerId = gamePlayer.Id,
			HandNumber = game.CurrentHandNumber,
			DealOrder = dealOrder,
			Location = location,
			IsVisible = location != CardLocation.Hole,
			Symbol = symbol,
			Suit = suit,
			DealtAt = DateTimeOffset.UtcNow,
			DealtAtPhase = nameof(Phases.ThirdStreet)
		};
	}

	private static GameCard CreateDeckCard(Game game, int dealOrder, CardSymbol symbol, CardSuit suit)
	{
		return new GameCard
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			DealOrder = dealOrder,
			Location = CardLocation.Deck,
			IsVisible = false,
			Symbol = symbol,
			Suit = suit,
			DealtAt = DateTimeOffset.UtcNow,
			DealtAtPhase = nameof(Phases.ThirdStreet)
		};
	}

	private static Pot CreateMainPot(Game game)
	{
		return new Pot
		{
			Id = Guid.NewGuid(),
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 0,
			IsAwarded = false,
			CreatedAt = DateTimeOffset.UtcNow
		};
	}
}