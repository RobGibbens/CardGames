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
	IPlayerChipWalletService playerChipWalletService,
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

		if (command.StartingChips <= 0)
		{
			return new JoinGameError(
				JoinGameErrorCode.InvalidStartingChips,
				"Starting chips must be greater than 0.");
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

		var linkedLeagueId = await context.LeagueSeasonEvents
			.AsNoTracking()
			.Where(x => x.LaunchedGameId == game.Id)
			.Select(x => (Guid?)x.LeagueId)
			.FirstOrDefaultAsync(cancellationToken);

		if (!linkedLeagueId.HasValue)
		{
			linkedLeagueId = await context.LeagueOneOffEvents
				.AsNoTracking()
				.Where(x => x.LaunchedGameId == game.Id)
				.Select(x => (Guid?)x.LeagueId)
				.FirstOrDefaultAsync(cancellationToken);
		}

		if (linkedLeagueId.HasValue)
		{
			var isActiveLeagueMember = !string.IsNullOrWhiteSpace(currentUserService.UserId) &&
				await context.LeagueMembersCurrent
					.AsNoTracking()
					.AnyAsync(
						x => x.LeagueId == linkedLeagueId.Value &&
							x.UserId == currentUserService.UserId &&
							x.IsActive,
						cancellationToken);

			if (!isActiveLeagueMember)
			{
				return new JoinGameError(
					JoinGameErrorCode.LeagueMembershipRequired,
					"Only active league members can join this league event table.");
			}
		}

		// Check if game has ended
		if (game.Status == GameStatus.Completed || game.Status == GameStatus.Cancelled)
		{
			return new JoinGameError(
				JoinGameErrorCode.GameEnded,
				"This game has ended and is no longer accepting players.");
		}

		// Check max players (Dealer's Choice games have no GameType; default to 8 seats)
		var maxPlayers = game.GameType?.MaxPlayers ?? MaxSeatIndex + 1;
		var activePlayerCount = game.GamePlayers.Count(gp =>
			gp.Status == GamePlayerStatus.Active ||
			gp.Status == GamePlayerStatus.SittingOut);

		if (activePlayerCount >= maxPlayers)
		{
			return new JoinGameError(
				JoinGameErrorCode.MaxPlayersReached,
				$"This game has reached the maximum of {maxPlayers} players.");
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

		// Check if the player has any chips at all
		var accountBalance = await playerChipWalletService.GetBalanceAsync(player.Id, cancellationToken);
		if (accountBalance <= 0)
		{
			return new JoinGameError(
				JoinGameErrorCode.ZeroAccountBalance,
				"You have no chips in your account. Visit the Cashier to add chips before joining a table.");
		}

		var now = DateTimeOffset.UtcNow;

		var debitResult = await playerChipWalletService.TryDebitForBuyInAsync(
			player.Id,
			command.StartingChips,
			game.Id,
			currentUserService.UserId,
			cancellationToken);

		if (!debitResult.Succeeded)
		{
			return new JoinGameError(
				JoinGameErrorCode.InsufficientAccountChips,
				debitResult.ErrorMessage ?? "Insufficient chips in your account.");
		}

		// Create the game participation record
		var gamePlayer = new GamePlayer
		{
			GameId = game.Id,
			PlayerId = player.Id,
			SeatPosition = command.SeatIndex,
			ChipStack = command.StartingChips,
			StartingChips = command.StartingChips,
			BringInAmount = command.StartingChips,
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
					PlayerId: player.Id,
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
