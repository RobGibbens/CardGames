using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.JoinGame;

/// <summary>
/// Handles the JoinGame command to add a player to a specific seat in a game.
/// </summary>
public sealed class JoinGameCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	IGameStateBroadcaster broadcaster,
	ILogger<JoinGameCommandHandler> logger)
	: IRequestHandler<JoinGameCommand, OneOf<JoinGameSuccessful, JoinGameError>>
{
	private const int MaxSeatIndex = 7; // 0-based, so 8 seats total

	/// <inheritdoc />
	public async Task<OneOf<JoinGameSuccessful, JoinGameError>> Handle(
		JoinGameCommand command,
		CancellationToken cancellationToken)
	{
		var playerName = currentUserService.UserName;
		if (string.IsNullOrWhiteSpace(playerName))
		{
			return new JoinGameError(
				JoinGameErrorCode.GameNotFound,
				"User is not authenticated.");
		}

		// Validate seat index
		if (command.SeatIndex < 0 || command.SeatIndex > MaxSeatIndex)
		{
			return new JoinGameError(
				JoinGameErrorCode.InvalidSeatIndex,
				$"Seat index must be between 0 and {MaxSeatIndex}.");
		}

		// Load the game with game type and current players
		var game = await context.Games
			.Include(g => g.GameType)
			.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
		{
			return new JoinGameError(
				JoinGameErrorCode.GameNotFound,
				$"Game with ID {command.GameId} was not found.");
		}

		// Check if game has ended
		if (game.Status == GameStatus.Completed || game.Status == GameStatus.Cancelled)
		{
			return new JoinGameError(
				JoinGameErrorCode.GameEnded,
				"This game has ended and is no longer accepting players.");
		}

		// Check max players
		var activePlayerCount = game.GamePlayers.Count(gp =>
			gp.Status == GamePlayerStatus.Active ||
			gp.Status == GamePlayerStatus.SittingOut);

		if (activePlayerCount >= game.GameType.MaxPlayers)
		{
			return new JoinGameError(
				JoinGameErrorCode.MaxPlayersReached,
				$"This game has reached the maximum of {game.GameType.MaxPlayers} players.");
		}

		// Check if player is already seated in this game
		var existingGamePlayer = game.GamePlayers.FirstOrDefault(gp =>
			gp.Player.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase) &&
			(gp.Status == GamePlayerStatus.Active || gp.Status == GamePlayerStatus.SittingOut));

		if (existingGamePlayer is not null)
		{
			return new JoinGameError(
				JoinGameErrorCode.AlreadySeated,
				$"You are already seated at position {existingGamePlayer.SeatPosition}.");
		}

		// Check if the seat is already occupied
		var seatOccupied = game.GamePlayers.Any(gp =>
			gp.SeatPosition == command.SeatIndex &&
			(gp.Status == GamePlayerStatus.Active || gp.Status == GamePlayerStatus.SittingOut));

		if (seatOccupied)
		{
			return new JoinGameError(
				JoinGameErrorCode.SeatOccupied,
				$"Seat {command.SeatIndex} is already occupied.");
		}

		// Determine if player can play the current hand
		// They can only play if game is in WaitingToStart or WaitingForPlayers phase
		var canPlayCurrentHand = game.CurrentPhase == nameof(Phases.WaitingToStart) ||
								 game.Status == GameStatus.WaitingForPlayers;

		var userEmail = currentUserService.UserEmail;

		// Get or create the player entity
		var player = await GetOrCreatePlayerAsync(playerName, userEmail, cancellationToken);

		var userProfile = !string.IsNullOrWhiteSpace(userEmail)
			? await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == userEmail, cancellationToken)
			: null;

		var now = DateTimeOffset.UtcNow;

		// Create the game participation record
		var gamePlayer = new GamePlayer
		{
			GameId = game.Id,
			PlayerId = player.Id,
			SeatPosition = command.SeatIndex,
			ChipStack = command.StartingChips,
			StartingChips = command.StartingChips,
			CurrentBet = 0,
			TotalContributedThisHand = 0,
			HasFolded = !canPlayCurrentHand, // If mid-hand, they're folded out of current hand
			IsAllIn = false,
			IsConnected = true,
			IsSittingOut = false,
			HasDrawnThisRound = false,
			JoinedAtHandNumber = game.CurrentHandNumber,
			LeftAtHandNumber = -1,
			Status = GamePlayerStatus.Active,
			JoinedAt = now
		};

		context.GamePlayers.Add(gamePlayer);
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);

		logger.LogInformation(
			"Player {PlayerName} joined game {GameId} at seat {SeatIndex}. CanPlayCurrentHand: {CanPlay}",
			playerName, command.GameId, command.SeatIndex, canPlayCurrentHand);

		// Broadcast player joined notification to other players (excluding the one who just joined)
		await broadcaster.BroadcastPlayerJoinedAsync(
			game.Id,
			playerName,
			command.SeatIndex,
			canPlayCurrentHand,
			cancellationToken);

		return new JoinGameSuccessful(
			GameId: game.Id,
			SeatIndex: command.SeatIndex,
			PlayerName: playerName,
			PlayerAvatarUrl: userProfile?.AvatarUrl ?? player.AvatarUrl,
			PlayerFirstName: userProfile?.FirstName,
			CanPlayCurrentHand: canPlayCurrentHand);
	}

	private async Task<Player> GetOrCreatePlayerAsync(string name, string? email, CancellationToken cancellationToken)
	{
		var player = await context.Players
			.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

		if (player is not null)
		{
			// Update email if not previously set (supports existing players before email tracking)
			if (string.IsNullOrWhiteSpace(player.Email) && !string.IsNullOrWhiteSpace(email))
			{
				player.Email = email;
			}

			return player;
		}

		var now = DateTimeOffset.UtcNow;
		player = new Player
		{
			Name = name,
			Email = email,
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
