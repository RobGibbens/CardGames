using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.LeaveGame;

/// <summary>
/// Handles the LeaveGame command to remove a player from a game table.
/// </summary>
public sealed class LeaveGameCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	IGameStateBroadcaster broadcaster,
	ILogger<LeaveGameCommandHandler> logger)
	: IRequestHandler<LeaveGameCommand, OneOf<LeaveGameSuccessful, LeaveGameError>>
{
	/// <inheritdoc />
	public async Task<OneOf<LeaveGameSuccessful, LeaveGameError>> Handle(
		LeaveGameCommand command,
		CancellationToken cancellationToken)
	{
		// 1. Get the current authenticated user
		var currentUserId = currentUserService.UserId;
		var currentUserName = currentUserService.UserName;
		
		if (string.IsNullOrWhiteSpace(currentUserId) || string.IsNullOrWhiteSpace(currentUserName))
		{
			return new LeaveGameError("User not authenticated");
		}

		// 2. Load the game with related entities
		var game = await context.Games
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Cards)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game == null)
		{
			return new LeaveGameError("Game not found");
		}

		// 3. Find the player's GamePlayer record
		var gamePlayer = game.GamePlayers
			.FirstOrDefault(gp => 
				gp.Player.Name.Equals(currentUserName, StringComparison.OrdinalIgnoreCase) && 
				gp.Status != GamePlayerStatus.Left);

		if (gamePlayer == null)
		{
			return new LeaveGameError("You are not seated at this table or have already left");
		}

		// 4. Check if game has started
		var gameStarted = game.StartedAt.HasValue && game.Status == GameStatus.InProgress;

		// Consistent player name for response
		var playerName = gamePlayer.Player.Email ?? gamePlayer.Player.Name;

		// 5. Handle pre-game departure (complete deletion)
		if (!gameStarted)
		{
			logger.LogInformation(
				"Player {PlayerName} leaving game {GameId} before game started - removing player record",
				currentUserName, game.Id);

			context.GamePlayers.Remove(gamePlayer);
			game.UpdatedAt = DateTimeOffset.UtcNow;
			await context.SaveChangesAsync(cancellationToken);

			await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);

			return new LeaveGameSuccessful(
				GameId: game.Id,
				PlayerId: gamePlayer.PlayerId,
				PlayerName: playerName,
				LeftAtHandNumber: -1,
				LeftAt: DateTimeOffset.UtcNow,
				FinalChipCount: null,
				Immediate: true);
		}

		// 6. Check if player is in an active hand
		var isInActiveHand = IsPlayerInActiveHand(gamePlayer, game);

		if (isInActiveHand)
		{
			// 7. Queue leave for end of hand
			logger.LogInformation(
				"Player {PlayerName} requested to leave game {GameId} during active hand - queuing for end of hand",
				currentUserName, game.Id);

			// Mark player to sit out (they'll finish current hand and then be marked as Left)
			gamePlayer.IsSittingOut = true;
			gamePlayer.LeftAtHandNumber = game.CurrentHandNumber;
			game.UpdatedAt = DateTimeOffset.UtcNow;

			await context.SaveChangesAsync(cancellationToken);

			await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);

			return new LeaveGameSuccessful(
				GameId: game.Id,
				PlayerId: gamePlayer.PlayerId,
				PlayerName: playerName,
				LeftAtHandNumber: -1,
				LeftAt: null,
				FinalChipCount: null,
				Immediate: false,
				Message: "You will leave the table after the current hand completes.");
		}

		// 8. Handle mid-game departure (not in active hand)
		logger.LogInformation(
			"Player {PlayerName} leaving game {GameId} between hands - marking as Left",
			currentUserName, game.Id);

		var now = DateTimeOffset.UtcNow;
		gamePlayer.Status = GamePlayerStatus.Left;
		gamePlayer.LeftAtHandNumber = game.CurrentHandNumber;
		gamePlayer.LeftAt = now;
		gamePlayer.FinalChipCount = gamePlayer.ChipStack;
		gamePlayer.IsSittingOut = true;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);

		await broadcaster.BroadcastGameStateAsync(game.Id, cancellationToken);

		return new LeaveGameSuccessful(
			GameId: game.Id,
			PlayerId: gamePlayer.PlayerId,
			PlayerName: playerName,
			LeftAtHandNumber: gamePlayer.LeftAtHandNumber,
			LeftAt: gamePlayer.LeftAt,
			FinalChipCount: gamePlayer.FinalChipCount,
			Immediate: true);
	}

	private bool IsPlayerInActiveHand(GamePlayer gamePlayer, Game game)
	{
		// Player is in an active hand if:
		// 1. Game is in progress (not between hands)
		// 2. Player has not folded
		// 3. Current phase is an "active" phase (betting, drawing, decision)
		// 4. Player has cards dealt in the current hand

		if (game.Status != GameStatus.InProgress)
		{
			return false;
		}

		if (gamePlayer.HasFolded)
		{
			return false;
		}

		// Check if current phase is an "active" phase (betting, drawing, decision)
		var activePhases = new[]
		{
			"Dealing", "PreFlop", "Flop", "Turn", "River",
			"FirstBettingRound", "SecondBettingRound", "ThirdBettingRound", "FourthBettingRound",
			"DrawPhase", "Drawing",
			"DropOrStay", "PlayerVsDeck",
			"ThirdStreet", "FourthStreet", "FifthStreet", "SixthStreet", "SeventhStreet"
		};

		if (!activePhases.Contains(game.CurrentPhase, StringComparer.OrdinalIgnoreCase))
		{
			return false;
		}

		// Check if player has cards for current hand
		var hasCardsInCurrentHand = gamePlayer.Cards
			.Any(c => c.HandNumber == game.CurrentHandNumber);

		return hasCardsInCurrentHand;
	}
}
