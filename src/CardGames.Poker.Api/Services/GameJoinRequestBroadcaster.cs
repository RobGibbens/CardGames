using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Services;

public sealed class GameJoinRequestBroadcaster(
	IHubContext<NotificationHub> hubContext,
	ILogger<GameJoinRequestBroadcaster> logger)
	: IGameJoinRequestBroadcaster
{
	public async Task BroadcastJoinRequestReceivedAsync(string hostUserRoutingKey, GameJoinRequestReceivedDto payload, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(hostUserRoutingKey);
		ArgumentNullException.ThrowIfNull(payload);

		await hubContext.Clients.User(hostUserRoutingKey)
			.SendAsync("GameJoinRequestReceived", payload, cancellationToken);

		logger.LogInformation(
			"Broadcast GameJoinRequestReceived for game {GameId}, request {JoinRequestId} to host {HostUserRoutingKey}",
			payload.GameId,
			payload.JoinRequestId,
			hostUserRoutingKey);
	}

	public async Task BroadcastJoinRequestResolvedAsync(string playerUserRoutingKey, GameJoinRequestResolvedDto payload, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(playerUserRoutingKey);
		ArgumentNullException.ThrowIfNull(payload);

		await hubContext.Clients.User(playerUserRoutingKey)
			.SendAsync("GameJoinRequestResolved", payload, cancellationToken);

		logger.LogInformation(
			"Broadcast GameJoinRequestResolved for game {GameId}, request {JoinRequestId} to player {PlayerUserRoutingKey} with status {Status}",
			payload.GameId,
			payload.JoinRequestId,
			playerUserRoutingKey,
			payload.Status);
	}
}