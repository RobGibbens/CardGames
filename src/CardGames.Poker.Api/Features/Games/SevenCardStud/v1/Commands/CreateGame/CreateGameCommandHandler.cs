using Azure.Messaging.ServiceBus;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Games.SevenCardStud;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.CreateGame;

public class CreateGameCommandHandler(CardsDbContext context,
	ServiceBusClient sbClient, HybridCache hybridCache, ICurrentUserService currentUserService)
	: IRequestHandler<CreateGameCommand, OneOf<CreateGameSuccessful, CreateGameConflict>>
{
	private const string SevenCardStudCode = "SEVENCARDSTUD";

	public async Task<OneOf<CreateGameSuccessful, CreateGameConflict>> Handle(CreateGameCommand command, CancellationToken cancellationToken)
	{
		if (command.GameId == Guid.Empty)
		{
			return new CreateGameConflict
			{
				GameId = command.GameId,
				Reason = "GameId must be a non-empty GUID."
			};
		}

		var existingGame = await context.Games
			.AsNoTracking()
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (existingGame is not null)
		{
			return new CreateGameConflict
			{
				GameId = command.GameId,
				Reason = "A game with the supplied GameId already exists."
			};
		}

		var now = DateTimeOffset.UtcNow;

		// 1. Get or create the Seven Card Stud game type
		var gameType = await GetOrCreateGameTypeAsync(cancellationToken);

		// 2. Create the game session
		var game = new Game
		{
			Id = command.GameId,
			GameTypeId = gameType.Id,
			Name = command.GameName,
			CurrentPhase = nameof(SevenCardStudPhase.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = command.Ante,
			BringIn = command.BringIn,
			SmallBet = command.SmallBet,
			BigBet = command.BigBet,
			Status = GameStatus.WaitingForPlayers,
			CurrentPlayerIndex = -1,
			BringInPlayerIndex = -1,
			CreatedById = currentUserService.UserId,
			CreatedByName = currentUserService.UserName,
			CreatedAt = now,
			UpdatedAt = now
		};

		context.Games.Add(game);

		// 3. Get or create players and add them to the game
		var seatPosition = 0;
		foreach (var playerInfo in command.Players)
		{
			// Find existing player or create new one
			var player = await GetOrCreatePlayerAsync(playerInfo.Name, cancellationToken);

			// Create the game participation record
			var gamePlayer = new GamePlayer
			{
				GameId = game.Id,
				Player = player,
				PlayerId = player.Id,
				SeatPosition = seatPosition,
				ChipStack = playerInfo.StartingChips,
				StartingChips = playerInfo.StartingChips,
				CurrentBet = 0,
				TotalContributedThisHand = 0,
				HasFolded = false,
				IsAllIn = false,
				IsConnected = true,
				IsSittingOut = false,
				HasDrawnThisRound = false,
				JoinedAtHandNumber = 1,
				LeftAtHandNumber = -1,
				Status = GamePlayerStatus.Active,
				JoinedAt = now
			};

			context.GamePlayers.Add(gamePlayer);
			seatPosition++;
		}

		// 4. Create the initial main pot (empty, will be populated when antes are collected)
		var mainPot = new Pot
		{
			GameId = game.Id,
			HandNumber = 1,
			PotType = PotType.Main,
			PotOrder = 0,
			Amount = 0,
			IsAwarded = false,
			CreatedAt = now
		};

		context.Pots.Add(mainPot);

		await context.SaveChangesAsync(cancellationToken);

		return new CreateGameSuccessful
		{
			GameId = game.Id,
			GameTypeCode = gameType.Code,
			PlayerCount = command.Players.Count
		};
	}

	private async Task<GameType> GetOrCreateGameTypeAsync(CancellationToken cancellationToken)
	{
		var gameType = await context.GameTypes
			.FirstOrDefaultAsync(gt => gt.Code == SevenCardStudCode, cancellationToken);

		if (gameType is not null)
		{
			return gameType;
		}

		// Create the Seven Card Stud game type definition
		var now = DateTimeOffset.UtcNow;
		gameType = new GameType
		{
			Name = "Seven Card Stud",
			Code = SevenCardStudCode,
			Description = "A classic poker variant where players receive seven cards individually (3 down, 4 up), " +
						  "with betting rounds after the third, fourth, fifth, sixth, and seventh cards.",
			BettingStructure = BettingStructure.AnteBringIn,
			MinPlayers = 2,
			MaxPlayers = 7,
			InitialHoleCards = 2,  // Initial 2 hole cards
			InitialBoardCards = 1, // Initial 1 board card (up)
			MaxCommunityCards = 0,
			MaxPlayerCards = 7,
			HasDrawPhase = false,
			MaxDiscards = 0,
			WildCardRule = WildCardRule.None,
			IsActive = true,
			CreatedAt = now,
			UpdatedAt = now
		};

		context.GameTypes.Add(gameType);
		return gameType;
	}

	private async Task<Player> GetOrCreatePlayerAsync(string name, CancellationToken cancellationToken)
	{
		var player = await context.Players
			.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

		if (player is not null)
		{
			return player;
		}

		var now = DateTimeOffset.UtcNow;
		player = new Player
		{
			Name = name,
			Email = name,
			IsActive = true,
			TotalGamesPlayed = 0,
			TotalHandsPlayed = 0,
			TotalHandsWon = 0,
			TotalChipsWon = 0,
			TotalChipsLost = 0,
			CreatedAt = now,
			UpdatedAt = now
		};

		context.Players.Add(player);
		return player;
	}
}
