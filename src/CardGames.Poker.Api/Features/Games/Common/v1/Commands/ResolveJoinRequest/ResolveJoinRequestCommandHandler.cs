using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Common;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Api.Infrastructure;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Services.InMemoryEngine;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ResolveJoinRequest;

public sealed class ResolveJoinRequestCommandHandler(
	CardsDbContext context,
	ICurrentUserService currentUserService,
	IPlayerChipWalletService playerChipWalletService,
	IGameStateBroadcaster gameStateBroadcaster,
	IGameJoinRequestBroadcaster joinRequestBroadcaster,
	IOptions<InMemoryEngineOptions> engineOptions,
	IGameStateManager gameStateManager,
	ILogger<ResolveJoinRequestCommandHandler> logger)
	: IRequestHandler<ResolveJoinRequestCommand, OneOf<ResolveJoinRequestSuccessful, ResolveJoinRequestError>>
{
	private const int MaxSeatIndex = 7;

	public async Task<OneOf<ResolveJoinRequestSuccessful, ResolveJoinRequestError>> Handle(ResolveJoinRequestCommand command, CancellationToken cancellationToken)
	{
		var joinRequest = await context.GameJoinRequests
			.Include(x => x.Game)
				.ThenInclude(g => g.GameType)
			.Include(x => x.Game)
				.ThenInclude(g => g.GamePlayers)
			.Include(x => x.Player)
			.FirstOrDefaultAsync(x => x.GameId == command.GameId && x.Id == command.JoinRequestId, cancellationToken);

		if (joinRequest is null)
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.NotFound, "Join request was not found.");
		}

		var game = joinRequest.Game;
		if (!string.Equals(game.CreatedById, currentUserService.UserId, StringComparison.Ordinal))
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.NotHost, "Only the table host can resolve join requests.");
		}

		if (game.Status is GameStatus.Completed or GameStatus.Cancelled)
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.GameEnded, "This table is no longer accepting join approvals.");
		}

		if (joinRequest.Status != GameJoinRequestStatus.Pending)
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.AlreadyResolved, "This join request has already been resolved.");
		}

		var now = DateTimeOffset.UtcNow;
		if (joinRequest.ExpiresAt <= now)
		{
			joinRequest.Status = GameJoinRequestStatus.Expired;
			joinRequest.UpdatedAt = now;
			joinRequest.ResolvedAt = now;
			joinRequest.ResolvedByUserId = currentUserService.UserId;
			joinRequest.ResolvedByName = currentUserService.UserName;
			joinRequest.ResolutionReason = "This join request expired before the host responded.";
			await context.SaveChangesAsync(cancellationToken);
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.Expired, "This join request has expired.");
		}

		if (!command.Approved)
		{
			joinRequest.Status = GameJoinRequestStatus.Denied;
			joinRequest.UpdatedAt = now;
			joinRequest.ResolvedAt = now;
			joinRequest.ResolvedByUserId = currentUserService.UserId;
			joinRequest.ResolvedByName = currentUserService.UserName;
			joinRequest.ResolutionReason = string.IsNullOrWhiteSpace(command.DenialReason)
				? "The host denied the join request."
				: command.DenialReason.Trim();

			await context.SaveChangesAsync(cancellationToken);
			await NotifyRequesterAsync(joinRequest, game, now, cancellationToken);

			return new ResolveJoinRequestSuccessful(game.Id, joinRequest.Id, joinRequest.Status.ToString(), null, null);
		}

		var approvedBuyIn = command.ApprovedBuyIn ?? joinRequest.RequestedBuyIn;
		if (approvedBuyIn <= 0 || approvedBuyIn > joinRequest.RequestedBuyIn)
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.InvalidApprovedBuyIn, "Approved buy-in must be greater than 0 and cannot exceed the requested amount.");
		}

		if (game.MaxBuyIn.HasValue && approvedBuyIn > game.MaxBuyIn.Value)
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.InvalidApprovedBuyIn, $"Approved buy-in cannot exceed the table maximum of {game.MaxBuyIn.Value:N0}.");
		}

		var lateJoinAllowed = !string.Equals(
				game.GameType?.Code,
				PokerGameMetadataRegistry.ScrewYourNeighborCode,
				StringComparison.OrdinalIgnoreCase)
			&& !string.Equals(
				game.GameType?.Code,
				PokerGameMetadataRegistry.InBetweenCode,
				StringComparison.OrdinalIgnoreCase)
			|| !game.StartedAt.HasValue;

		if (!lateJoinAllowed)
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.GameEnded, "This game variant no longer allows late joins.");
		}

		var maxPlayers = game.GameType?.MaxPlayers ?? MaxSeatIndex + 1;
		var activePlayerCount = game.GamePlayers.Count(gp => gp.Status is GamePlayerStatus.Active or GamePlayerStatus.SittingOut);
		if (activePlayerCount >= maxPlayers)
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.SeatUnavailable, "No seats are available anymore.");
		}

		var seatIndex = ResolveSeatIndex(game.GamePlayers, joinRequest.SeatIndex);
		if (seatIndex is null)
		{
			return new ResolveJoinRequestError(ResolveJoinRequestErrorCode.SeatUnavailable, "No seats are available anymore.");
		}

		var debitResult = await playerChipWalletService.TryDebitForBuyInAsync(
			joinRequest.PlayerId,
			approvedBuyIn,
			game.Id,
			currentUserService.UserId,
			cancellationToken);

		if (!debitResult.Succeeded)
		{
			return new ResolveJoinRequestError(
				ResolveJoinRequestErrorCode.InsufficientAccountChips,
				debitResult.ErrorMessage ?? "The player no longer has enough chips for this buy-in.");
		}

		var canPlayCurrentHand = string.Equals(game.CurrentPhase, "WaitingToStart", StringComparison.Ordinal) || game.Status == GameStatus.WaitingForPlayers;
		var gamePlayer = new GamePlayer
		{
			GameId = game.Id,
			PlayerId = joinRequest.PlayerId,
			SeatPosition = seatIndex.Value,
			ChipStack = approvedBuyIn,
			StartingChips = approvedBuyIn,
			BringInAmount = approvedBuyIn,
			CurrentBet = 0,
			TotalContributedThisHand = 0,
			HasFolded = !canPlayCurrentHand,
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
		joinRequest.ApprovedBuyIn = approvedBuyIn;
		joinRequest.Status = GameJoinRequestStatus.Approved;
		joinRequest.UpdatedAt = now;
		joinRequest.ResolvedAt = now;
		joinRequest.ResolvedByUserId = currentUserService.UserId;
		joinRequest.ResolvedByName = currentUserService.UserName;
		joinRequest.ResolutionReason = null;
		game.UpdatedAt = now;

		await context.SaveChangesAsync(cancellationToken);

		// Refresh in-memory state so subsequent handlers see the approved player
		if (engineOptions.Value.Enabled)
		{
			await gameStateManager.ReloadGameAsync(game.Id, cancellationToken);
		}

		await gameStateBroadcaster.BroadcastPlayerJoinedAsync(game.Id, joinRequest.Player.Name, seatIndex.Value, canPlayCurrentHand, cancellationToken);
		await NotifyRequesterAsync(joinRequest, game, now, cancellationToken);

		logger.LogInformation("Host {HostId} approved join request {JoinRequestId} for game {GameId}", currentUserService.UserId, joinRequest.Id, game.Id);

		return new ResolveJoinRequestSuccessful(game.Id, joinRequest.Id, joinRequest.Status.ToString(), approvedBuyIn, seatIndex);
	}

	private async Task NotifyRequesterAsync(GameJoinRequest joinRequest, Game game, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
	{
		var routingKey = joinRequest.Player.Email ?? joinRequest.Player.Name;
		if (string.IsNullOrWhiteSpace(routingKey))
		{
			return;
		}

		await joinRequestBroadcaster.BroadcastJoinRequestResolvedAsync(
			routingKey,
			new GameJoinRequestResolvedDto
			{
				GameId = game.Id,
				JoinRequestId = joinRequest.Id,
				Status = joinRequest.Status.ToString(),
				HostName = game.CreatedByName ?? "Host",
				PlayerName = joinRequest.Player.Name,
				ApprovedBuyIn = joinRequest.ApprovedBuyIn,
				Reason = joinRequest.ResolutionReason,
				ResolvedAtUtc = resolvedAt
			},
			cancellationToken);
	}

	private static int? ResolveSeatIndex(ICollection<GamePlayer> players, int requestedSeatIndex)
	{
		var occupiedSeats = players
			.Where(gp => gp.Status is GamePlayerStatus.Active or GamePlayerStatus.SittingOut)
			.Select(gp => gp.SeatPosition)
			.ToHashSet();

		if (!occupiedSeats.Contains(requestedSeatIndex))
		{
			return requestedSeatIndex;
		}

		for (var seatIndex = 0; seatIndex <= MaxSeatIndex; seatIndex++)
		{
			if (!occupiedSeats.Contains(seatIndex))
			{
				return seatIndex;
			}
		}

		return null;
	}
}