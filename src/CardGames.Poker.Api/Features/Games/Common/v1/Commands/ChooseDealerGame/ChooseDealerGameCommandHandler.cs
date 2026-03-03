using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ChooseDealerGame;

public class ChooseDealerGameCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService)
	: IRequestHandler<ChooseDealerGameCommand, OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>>
{
	public async Task<OneOf<ChooseDealerGameSuccessful, ChooseDealerGameError>> Handle(
		ChooseDealerGameCommand command,
		CancellationToken cancellationToken)
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

		var now = DateTimeOffset.UtcNow;

		// 7. Get or create the GameType entity for the chosen game
		var gameType = await GetOrCreateGameTypeAsync(command.GameTypeCode, metadata, cancellationToken);

		// 8. Update the game with the dealer's choice
		game.GameTypeId = gameType.Id;
		game.CurrentHandGameTypeCode = command.GameTypeCode;
		game.Ante = command.Ante;
		game.MinBet = command.MinBet;
		game.CurrentPhase = nameof(Phases.WaitingToStart);
		game.Status = GameStatus.BetweenHands;
		// Schedule auto-start so the background service picks up and deals the hand
		game.NextHandStartsAt = now.AddSeconds(3);
		game.UpdatedAt = now;

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
			MinBet = command.MinBet
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
}
