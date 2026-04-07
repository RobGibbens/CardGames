using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;
using Pot = CardGames.Poker.Api.Data.Entities.Pot;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;

public class CreateGameCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<CreateGameCommand, OneOf<CreateGameSuccessful, CreateGameConflict>>
{
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

		if (command is { SmallBlind: not null, BigBlind: not null } && command.BigBlind <= command.SmallBlind)
		{
			return new CreateGameConflict
			{
				GameId = command.GameId,
				Reason = "Big blind must be greater than small blind."
			};
		}

		if (command.MaxBuyIn.HasValue)
		{
			var minimumAllowedBuyIn = GetMinimumAllowedBuyIn(command);
			if (command.MaxBuyIn.Value < minimumAllowedBuyIn)
			{
				return new CreateGameConflict
				{
					GameId = command.GameId,
					Reason = $"Max buy-in must be at least {minimumAllowedBuyIn}."
				};
			}
		}

		if (command.TournamentBuyIn.HasValue && command.TournamentBuyIn.Value <= 0)
		{
			return new CreateGameConflict
			{
				GameId = command.GameId,
				Reason = "Tournament buy-in must be greater than 0."
			};
		}

		var now = DateTimeOffset.UtcNow;
		var allowedDealerChoiceGameCodes = DealersChoiceGameSettings.NormalizeGameCodes(command.AllowedDealerChoiceGameCodes);

		if (command.IsDealersChoice && command.AllowedDealerChoiceGameCodes is not null && allowedDealerChoiceGameCodes.Count == 0)
		{
			return new CreateGameConflict
			{
				GameId = command.GameId,
				Reason = "Dealer's Choice tables must allow at least one game variant."
			};
		}

		if (command.IsDealersChoice)
		{
			foreach (var allowedGameCode in allowedDealerChoiceGameCodes)
			{
				if (!PokerGameMetadataRegistry.TryGet(allowedGameCode, out _))
				{
					return new CreateGameConflict
					{
						GameId = command.GameId,
						Reason = $"Unknown Dealer's Choice game code '{allowedGameCode}'."
					};
				}
			}
		}

		// 1. For Dealer's Choice tables, skip game type lookup; for standard tables, resolve game type
		GameType? gameType = null;
		if (!command.IsDealersChoice)
		{
			gameType = await GetOrCreateGameTypeAsync(command.GameCode, cancellationToken);
			if (gameType is null)
			{
				return new CreateGameConflict
				{
					GameId = command.GameId,
					Reason = $"Unknown game code '{command.GameCode}'."
				};
			}
		}

		// 2. Create the game session
		var game = new Game
		{
			Id = command.GameId,
			GameTypeId = gameType?.Id,
			Name = command.GameName,
			CurrentPhase = nameof(Phases.WaitingToStart),
			CurrentHandNumber = 0,
			DealerPosition = 0,
			Ante = command.IsDealersChoice ? null : command.Ante,
			MinBet = command.IsDealersChoice ? null : command.MinBet,
			SmallBlind = command.SmallBlind,
			BigBlind = command.BigBlind,
			MaxBuyIn = command.MaxBuyIn,
			TournamentBuyIn = command.TournamentBuyIn,
			RequiresJoinApproval = command.RequiresJoinApproval,
			IsDealersChoice = command.IsDealersChoice,
			AreOddsVisibleToAllPlayers = command.AreOddsVisibleToAllPlayers,
			Status = GameStatus.WaitingForPlayers,
			CurrentPlayerIndex = -1,
			CurrentDrawPlayerIndex = -1,
			CreatedById = currentUserService.UserId,
			CreatedByName = currentUserService.UserName,
			CreatedAt = now,
			UpdatedAt = now
		};

		if (command.IsDealersChoice && allowedDealerChoiceGameCodes.Count > 0)
		{
			DealersChoiceGameSettings.SaveAllowedGameCodes(game, allowedDealerChoiceGameCodes);
		}

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
			GameTypeCode = gameType?.Code ?? "DEALERSCHOICE",
			PlayerCount = command.Players.Count
		};
	}

	private async Task<GameType?> GetOrCreateGameTypeAsync(string gameCode, CancellationToken cancellationToken)
	{
		if (!PokerGameMetadataRegistry.TryGet(gameCode, out var metadata) || metadata is null)
		{
			return null;
		}

		var gameType = await context.GameTypes
			.FirstOrDefaultAsync(gt => gt.Code == gameCode, cancellationToken);

		if (gameType is not null)
		{
			return gameType;
		}

		// Create the game type definition
		var now = DateTimeOffset.UtcNow;
		gameType = new GameType
		{
			Code = gameCode,
			Name = metadata.Name,
			Description = metadata.Description,
			BettingStructure = metadata.BettingStructure,
			MinPlayers = metadata.MinimumNumberOfPlayers,
			MaxPlayers = metadata.MaximumNumberOfPlayers,
			InitialHoleCards = metadata.InitialHoleCards,
			InitialBoardCards = metadata.InitialBoardCards,
			MaxCommunityCards = metadata.MaxCommunityCards,
			MaxPlayerCards = metadata.MaxPlayerCards,
			HasDrawPhase = metadata.HasDrawPhase,
			MaxDiscards = metadata.MaxDiscards,
			WildCardRule = metadata.WildCardRule,
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

	private static int GetMinimumAllowedBuyIn(CreateGameCommand command)
	{
		if (command.IsDealersChoice)
		{
			return 1;
		}

		if (string.Equals(command.GameCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase))
		{
			return Math.Max(1, command.Ante * 3);
		}

		var baseMinimum = Math.Max(command.Ante, command.BigBlind ?? command.MinBet);
		return Math.Max(1, baseMinimum);
	}
}