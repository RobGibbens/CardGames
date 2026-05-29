#nullable enable

using System;
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
using NSubstitute;
using Xunit;

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
}