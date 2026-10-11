using CardGames.Contracts.AddChips;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.AddChips;

/// <summary>
///     Handles the AddChips command to add chips to a player's stack in a game.
/// </summary>
public sealed class AddChipsCommandHandler(
	CardsDbContext context,
	IPlayerChipWalletService playerChipWalletService,
	ICurrentUserService currentUserService,
	IGamePlayerResolver gamePlayerResolver,
	IHttpContextAccessor httpContextAccessor,
	IGameStateBroadcaster broadcaster,
	ILogger<AddChipsCommandHandler> logger)
	: IRequestHandler<AddChipsCommand, OneOf<AddChipsResponse, AddChipsError>>
{
	/// <inheritdoc />
	public async Task<OneOf<AddChipsResponse, AddChipsError>> Handle(
		AddChipsCommand command,
		CancellationToken cancellationToken)
	{
		// Validate amount
		if (command.Amount <= 0)
			return new AddChipsError(
				AddChipsErrorCode.InvalidAmount,
				"Amount must be greater than 0.");

		// Load the game with game type and players
		var game = await context.Games
			.Include(g => g.GameType)
			.Include(g => g.GamePlayers)
			.ThenInclude(gp => gp.Player)
			.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

		if (game is null)
			return new AddChipsError(
				AddChipsErrorCode.GameNotFound,
				$"Game with ID {command.GameId} was not found.");

		// Check if game has ended
		if (game.Status == GameStatus.Completed || game.Status == GameStatus.Cancelled)
			return new AddChipsError(
				AddChipsErrorCode.GameEnded,
				"This game has ended and chips cannot be added.");

		if (game.TournamentBuyIn.HasValue)
			return new AddChipsError(
				AddChipsErrorCode.TournamentRebuyNotAllowed,
				"Tournament tables do not allow rebuys after play begins.");

		// Find the game player
		var gamePlayer = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
		if (gamePlayer is null)
			return new AddChipsError(
				AddChipsErrorCode.PlayerNotInGame,
				"Player is not part of this game.");

		if (gamePlayer.Status == GamePlayerStatus.Left)
			return new AddChipsError(
				AddChipsErrorCode.PlayerNotInGame,
				"Player has already left this game.");

		if (httpContextAccessor.HttpContext is not null)
		{
			if (!currentUserService.IsAuthenticated)
				return new AddChipsError(AddChipsErrorCode.NotAuthorized, "Authentication is required.");

			var seated = await gamePlayerResolver.ResolveSeatedPlayerAsync(command.GameId, cancellationToken);
			var isSelf = seated is not null && seated.PlayerId == command.PlayerId;
			var isHost = await gamePlayerResolver.IsTableHostAsync(command.GameId, cancellationToken);
			if (!isSelf && !isHost)
				return new AddChipsError(AddChipsErrorCode.NotAuthorized,
					"You can only add chips to your own stack unless you are the table host.");
		}

		// Determine if chips should be applied immediately.
		// During chip-related pauses and between-hands waiting states, apply immediately so play can resume.
		var applyImmediately = string.Equals(game.GameType.Code, PokerGameMetadataRegistry.KingsAndLowsCode,
			                       StringComparison.OrdinalIgnoreCase)
		                       || game.IsPausedForChipCheck
		                       || game.IsPausedForRebuyGrace
		                       || string.Equals(game.CurrentPhase, "BetweenHands", StringComparison.OrdinalIgnoreCase)
		                       || string.Equals(game.CurrentPhase, "WaitingForPlayers",
			                       StringComparison.OrdinalIgnoreCase);

		var update = await ApplyChipUpdateAsync(game, gamePlayer);
		if (update.IsT1)
			return update.AsT1;

		var message = update.AsT0;

		try
		{
			await context.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateConcurrencyException)
		{
			context.ChangeTracker.Clear();
			game = await context.Games
				.Include(g => g.GameType)
				.Include(g => g.GamePlayers)
				.ThenInclude(gp => gp.Player)
				.FirstOrDefaultAsync(g => g.Id == command.GameId, cancellationToken);

			if (game is null)
				return new AddChipsError(
					AddChipsErrorCode.GameNotFound,
					$"Game with ID {command.GameId} was not found.");

			if (game.Status == GameStatus.Completed || game.Status == GameStatus.Cancelled)
				return new AddChipsError(
					AddChipsErrorCode.GameEnded,
					"This game has ended and chips cannot be added.");

			gamePlayer = game.GamePlayers.FirstOrDefault(gp => gp.PlayerId == command.PlayerId);
			if (gamePlayer is null || gamePlayer.Status == GamePlayerStatus.Left)
				return new AddChipsError(
					AddChipsErrorCode.PlayerNotInGame,
					gamePlayer is null
						? "Player is not part of this game."
						: "Player has already left this game.");

			update = await ApplyChipUpdateAsync(game, gamePlayer);
			if (update.IsT1)
				return update.AsT1;

			message = update.AsT0;
			await context.SaveChangesAsync(cancellationToken);
		}

		// Broadcast state update
		await broadcaster.BroadcastGameStateAsync(command.GameId, cancellationToken);

		return new AddChipsResponse
		{
			NewChipStack = gamePlayer.ChipStack,
			PendingChipsToAdd = gamePlayer.PendingChipsToAdd,
			AppliedImmediately = applyImmediately,
			Message = message
		};

		async Task<OneOf<string, AddChipsError>> ApplyChipUpdateAsync(Game targetGame, GamePlayer targetPlayer)
		{
			if (applyImmediately)
			{
				var debit = await playerChipWalletService.TryDebitForBuyInAsync(
					targetPlayer.PlayerId,
					command.Amount,
					command.GameId,
					currentUserService.UserId,
					cancellationToken);
				if (!debit.Succeeded)
					return new AddChipsError(
						AddChipsErrorCode.InsufficientAccountChips,
						debit.ErrorMessage ?? "Insufficient chips in your account.");

				targetPlayer.ChipStack += command.Amount;
				targetPlayer.BringInAmount += command.Amount;
				targetPlayer.IsSittingOut = false;
				if (targetPlayer.Status == GamePlayerStatus.SittingOut)
					targetPlayer.Status = GamePlayerStatus.Active;

				if (targetGame.IsPausedForRebuyGrace)
					targetGame.UpdatedAt = DateTimeOffset.UtcNow;

				logger.LogInformation(
					"Added {Amount} chips immediately to player {PlayerId} in game {GameId}",
					command.Amount, command.PlayerId, command.GameId);
				return $"{command.Amount} chips added to your stack.";
			}

			var balance = await playerChipWalletService.GetBalanceAsync(targetPlayer.PlayerId, cancellationToken);
			var requested = targetPlayer.PendingChipsToAdd + command.Amount;
			if (balance < requested)
				return new AddChipsError(
					AddChipsErrorCode.InsufficientAccountChips,
					$"Insufficient chips in your account. Balance: {balance:N0}, requested: {requested:N0}.");

			targetPlayer.PendingChipsToAdd += command.Amount;
			logger.LogInformation(
				"Queued {Amount} chips for player {PlayerId} in game {GameId} (total pending: {PendingChips})",
				command.Amount, command.PlayerId, command.GameId, targetPlayer.PendingChipsToAdd);
			return $"{command.Amount} chips will be added at the start of the next hand.";
		}
	}
}