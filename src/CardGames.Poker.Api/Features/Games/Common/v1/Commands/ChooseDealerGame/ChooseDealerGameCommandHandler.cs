using System.Text.Json;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services.InMemoryEngine;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;

public class ChooseDealerGameCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameExecutionCoordinator coordinator)
	: IRequestHandler<ChooseDealerGameCommand, OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>>
{
	public async Task<OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>> Handle(
		ChooseDealerGameCommand command,
		CancellationToken cancellationToken)
	{
		if (engineOptions.Value.Enabled)
			return await HandleInMemory(command, cancellationToken);

		return await HandleDatabase(command, cancellationToken);
	}

	private async Task<OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>> HandleInMemory(
		ChooseDealerGameCommand command, CancellationToken cancellationToken)
	{
		return await coordinator.ExecuteAsync(command.GameId, async (state, ct) =>
		{
			if (!state.IsDealersChoice)
			{
				return (OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>)new ChooseDealerGameError
				{
					GameId = command.GameId,
					Reason = "This is not a Dealer's Choice table."
				};
			}

			if (state.CurrentPhase != nameof(Phases.WaitingForDealerChoice))
			{
				return new ChooseDealerGameError
				{
					GameId = command.GameId,
					Reason = $"Game is not waiting for a dealer choice. Current phase: {state.CurrentPhase}."
				};
			}

			var dcDealerPosition = state.DealersChoiceDealerPosition;
			if (dcDealerPosition is null)
			{
				return new ChooseDealerGameError
				{
					GameId = command.GameId,
					Reason = "Dealer's Choice dealer position is not set."
				};
			}

			var dcDealer = state.Players
				.FirstOrDefault(p => p.SeatPosition == dcDealerPosition.Value && p.Status == GamePlayerStatus.Active);

			if (dcDealer is null)
			{
				return new ChooseDealerGameError
				{
					GameId = command.GameId,
					Reason = "Dealer's Choice dealer not found at the designated seat."
				};
			}

			var currentUserName = currentUserService.UserName;
			if (string.IsNullOrWhiteSpace(currentUserName))
			{
				return new ChooseDealerGameError
				{
					GameId = command.GameId,
					Reason = "User not authenticated."
				};
			}

			var currentPlayer = state.Players
				.FirstOrDefault(p => p.PlayerName.Equals(currentUserName, StringComparison.OrdinalIgnoreCase) && p.Status == GamePlayerStatus.Active);

			if (currentPlayer is null || currentPlayer.SeatPosition != dcDealerPosition.Value)
			{
				return new ChooseDealerGameError
				{
					GameId = command.GameId,
					Reason = "Only the current dealer may choose the game type."
				};
			}

			if (!PokerGameMetadataRegistry.TryGet(command.GameTypeCode, out var metadata) || metadata is null)
			{
				return new ChooseDealerGameError
				{
					GameId = command.GameId,
					Reason = $"Unknown game type code '{command.GameTypeCode}'."
				};
			}

			var allowedCodes = DealersChoiceGameSettings.GetAllowedGameCodes(state.GameSettings);
			if (allowedCodes is not null && !allowedCodes.Contains(command.GameTypeCode, StringComparer.OrdinalIgnoreCase))
			{
				return new ChooseDealerGameError
				{
					GameId = command.GameId,
					Reason = $"Game type '{command.GameTypeCode}' is not allowed at this Dealer's Choice table."
				};
			}

			var validationError = ValidateBlindAndAnte(command);
			if (validationError is not null)
			{
				return (OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>)validationError;
			}

			var now = DateTimeOffset.UtcNow;

			// DB operations: GameType lookup + hand log
			var gameType = await GetOrCreateGameTypeAsync(command.GameTypeCode, metadata, ct);

			state.GameTypeId = gameType.Id;
			state.CurrentHandGameTypeCode = command.GameTypeCode;
			state.Ante = command.Ante;
			state.MinBet = command.MinBet;

			if (command.SmallBlind.HasValue)
				state.SmallBlind = command.SmallBlind.Value;
			if (command.BigBlind.HasValue)
				state.BigBlind = command.BigBlind.Value;

			if (string.Equals(command.GameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase)
			    && command.Ante > 0)
			{
				var synBuyIn = command.Ante * 3;
				foreach (var p in state.Players.Where(p => p.Status == GamePlayerStatus.Active && !p.IsSittingOut))
				{
					p.VariantState = JsonSerializer.Serialize(new { preSynChips = p.ChipStack });
					p.ChipStack = synBuyIn;
				}
			}

			state.CurrentPhase = nameof(Phases.WaitingToStart);
			state.Status = GameStatus.BetweenHands;
			state.NextHandStartsAt = now.AddSeconds(3);
			state.UpdatedAt = now;
			state.OriginalDealersChoiceDealerPosition = state.DealersChoiceDealerPosition;

			var handLog = new DealersChoiceHandLog
			{
				GameId = state.GameId,
				HandNumber = state.CurrentHandNumber,
				GameTypeCode = command.GameTypeCode,
				GameTypeName = metadata.Name,
				DealerPlayerId = currentPlayer.PlayerId,
				DealerSeatPosition = currentPlayer.SeatPosition,
				Ante = command.Ante,
				MinBet = command.MinBet,
				SmallBlind = command.SmallBlind,
				BigBlind = command.BigBlind,
				ChosenAtUtc = now
			};
			context.DealersChoiceHandLogs.Add(handLog);
			await context.SaveChangesAsync(ct);

			return (OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>)new ChooseDealerGameSuccessful
			{
				GameId = state.GameId,
				GameTypeCode = command.GameTypeCode,
				GameTypeName = metadata.Name,
				HandNumber = state.CurrentHandNumber,
				Ante = command.Ante,
				MinBet = command.MinBet,
				SmallBlind = command.SmallBlind,
				BigBlind = command.BigBlind
			};
		}, cancellationToken);
	}

	private async Task<OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>> HandleDatabase(
		ChooseDealerGameCommand command, CancellationToken cancellationToken)
	{
		// 1. Load the game with players
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Game not found."
			};
		}

		// 2. Verify game is a Dealer's Choice table
		if (!game.IsDealersChoice)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "This is not a Dealer's Choice table."
			};
		}

		// 3. Verify game is in the WaitingForDealerChoice phase
		if (game.CurrentPhase != nameof(Phases.WaitingForDealerChoice))
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = $"Game is not waiting for a dealer choice. Current phase: {game.CurrentPhase}."
			};
		}

		// 4. Verify the caller is the current DC dealer
		var dcDealerPosition = game.DealersChoiceDealerPosition;
		if (dcDealerPosition is null)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Dealer's Choice dealer position is not set."
			};
		}

		var dcDealer = game.GamePlayers
			.FirstOrDefault(gp => gp.SeatPosition == dcDealerPosition.Value
								  && gp.Status == GamePlayerStatus.Active);

		if (dcDealer is null)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Dealer's Choice dealer not found at the designated seat."
			};
		}

		var currentUserName = currentUserService.UserName;
		if (string.IsNullOrWhiteSpace(currentUserName))
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "User not authenticated."
			};
		}

		var currentPlayer = game.GamePlayers
			.FirstOrDefault(gp => gp.Player != null
								  && gp.Player.Name.Equals(currentUserName, StringComparison.OrdinalIgnoreCase)
								  && gp.Status == GamePlayerStatus.Active);

		if (currentPlayer is null || currentPlayer.SeatPosition != dcDealerPosition.Value)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Only the current dealer may choose the game type."
			};
		}

		// 5. Validate the chosen game type code
		if (!PokerGameMetadataRegistry.TryGet(command.GameTypeCode, out var metadata) || metadata is null)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = $"Unknown game type code '{command.GameTypeCode}'."
			};
		}

		if (!DealersChoiceGameSettings.IsGameCodeAllowed(game, command.GameTypeCode))
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = $"Game type '{command.GameTypeCode}' is not allowed at this Dealer's Choice table."
			};
		}

		// 6. Validate ante / min bet are positive
		if (command.Ante < 0)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Ante must be zero or greater."
			};
		}

		if (command.MinBet <= 0)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Minimum bet must be greater than zero."
			};
		}

		// 6b. Validate blind values when provided
		if (command.SmallBlind.HasValue && command.SmallBlind.Value <= 0)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Small blind must be greater than zero."
			};
		}

		if (command.BigBlind.HasValue && command.BigBlind.Value <= 0)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Big blind must be greater than zero."
			};
		}

		if (command.SmallBlind.HasValue && command.BigBlind.HasValue
			&& command.BigBlind.Value < command.SmallBlind.Value)
		{
			return new ChooseDealerGameError
			{
				GameId = command.GameId,
				Reason = "Big blind must be greater than or equal to small blind."
			};
		}

		var now = DateTimeOffset.UtcNow;

		// 7. Get or create the GameType entity for the chosen game
		var gameType = await GetOrCreateGameTypeAsync(command.GameTypeCode, metadata, cancellationToken);

		// 8. Update the game with the dealer's choice
		game.GameTypeId = gameType.Id;
		game.CurrentHandGameTypeCode = command.GameTypeCode;
		game.Ante = command.Ante;
		game.MinBet = command.MinBet;

		// Apply blind values for Hold 'Em (or other blind-based games)
		if (command.SmallBlind.HasValue)
			game.SmallBlind = command.SmallBlind.Value;
		if (command.BigBlind.HasValue)
			game.BigBlind = command.BigBlind.Value;

		// For SYN in Dealer's Choice, snapshot DC chips and give each player 3 stacks.
		if (string.Equals(command.GameTypeCode, PokerGameMetadataRegistry.ScrewYourNeighborCode, StringComparison.OrdinalIgnoreCase)
		    && command.Ante > 0)
		{
			var synBuyIn = command.Ante * 3;
			foreach (var gp in game.GamePlayers.Where(gp => gp.Status == GamePlayerStatus.Active && !gp.IsSittingOut))
			{
				gp.VariantState = JsonSerializer.Serialize(new { preSynChips = gp.ChipStack });
				gp.ChipStack = synBuyIn;
			}
		}

		game.CurrentPhase = nameof(Phases.WaitingToStart);
		game.Status = GameStatus.BetweenHands;
		// Schedule auto-start so the background service picks up and deals the hand
		game.NextHandStartsAt = now.AddSeconds(3);
		game.UpdatedAt = now;

		// Track the original DC dealer so we can restore correctly when a multi-hand
		// variant like Kings and Lows finishes (DC dealer advances from the original picker).
		game.OriginalDealersChoiceDealerPosition = game.DealersChoiceDealerPosition;

		// 9. Log the choice
		var handLog = new DealersChoiceHandLog
		{
			GameId = game.Id,
			HandNumber = game.CurrentHandNumber,
			GameTypeCode = command.GameTypeCode,
			GameTypeName = metadata.Name,
			DealerPlayerId = currentPlayer.PlayerId,
			DealerSeatPosition = currentPlayer.SeatPosition,
			Ante = command.Ante,
			MinBet = command.MinBet,
			SmallBlind = command.SmallBlind,
			BigBlind = command.BigBlind,
			ChosenAtUtc = now
		};

		context.DealersChoiceHandLogs.Add(handLog);

		await context.SaveChangesAsync(cancellationToken);

		return new ChooseDealerGameSuccessful
		{
			GameId = game.Id,
			GameTypeCode = command.GameTypeCode,
			GameTypeName = metadata.Name,
			HandNumber = game.CurrentHandNumber,
			Ante = command.Ante,
			MinBet = command.MinBet,
			SmallBlind = command.SmallBlind,
			BigBlind = command.BigBlind
		};
	}

	private async Task<GameType> GetOrCreateGameTypeAsync(
		string gameCode,
		Poker.Games.PokerGameMetadataAttribute metadata,
		CancellationToken cancellationToken)
	{
		var gameType = await context.GameTypes
			.FirstOrDefaultAsync(gt => gt.Code == gameCode, cancellationToken);

		if (gameType is not null)
		{
			return gameType;
		}

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

	private static ChooseDealerGameError? ValidateBlindAndAnte(ChooseDealerGameCommand command)
	{
		if (command.Ante < 0)
			return new ChooseDealerGameError { GameId = command.GameId, Reason = "Ante must be zero or greater." };
		if (command.MinBet <= 0)
			return new ChooseDealerGameError { GameId = command.GameId, Reason = "Minimum bet must be greater than zero." };
		if (command.SmallBlind.HasValue && command.SmallBlind.Value <= 0)
			return new ChooseDealerGameError { GameId = command.GameId, Reason = "Small blind must be greater than zero." };
		if (command.BigBlind.HasValue && command.BigBlind.Value <= 0)
			return new ChooseDealerGameError { GameId = command.GameId, Reason = "Big blind must be greater than zero." };
		if (command.SmallBlind.HasValue && command.BigBlind.HasValue && command.BigBlind.Value < command.SmallBlind.Value)
			return new ChooseDealerGameError { GameId = command.GameId, Reason = "Big blind must be greater than or equal to small blind." };
		return null;
	}
}
