using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Infrastructure.Telemetry;
using Microsoft.AspNetCore.SignalR;
using System.Diagnostics;

namespace CardGames.Poker.Api.Services;

public sealed class GameJoinRequestBroadcaster(
	IHubContext<NotificationHub> hubContext,
	ILogger<GameJoinRequestBroadcaster> logger,
	BroadcastTelemetry telemetry)
	: IGameJoinRequestBroadcaster
{
	public async Task BroadcastJoinRequestReceivedAsync(string hostUserRoutingKey, GameJoinRequestReceivedDto payload, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(hostUserRoutingKey);
		ArgumentNullException.ThrowIfNull(payload);
		using var scope = CreateScope(payload.GameId, hostUserRoutingKey);

		await SendNotificationBroadcastAsync(
			hubContext.Clients.User(hostUserRoutingKey),
			"GameJoinRequestReceived",
			payload,
			payload.GameId,
			hostUserRoutingKey,
			cancellationToken);

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
		using var scope = CreateScope(payload.GameId, playerUserRoutingKey);

		await SendNotificationBroadcastAsync(
			hubContext.Clients.User(playerUserRoutingKey),
			"GameJoinRequestResolved",
			payload,
			payload.GameId,
			playerUserRoutingKey,
			cancellationToken);

		logger.LogInformation(
			"Broadcast GameJoinRequestResolved for game {GameId}, request {JoinRequestId} to player {PlayerUserRoutingKey} with status {Status}",
			payload.GameId,
			payload.JoinRequestId,
			playerUserRoutingKey,
			payload.Status);
	}

	private async Task SendNotificationBroadcastAsync(
		IClientProxy clientProxy,
		string eventName,
		object payload,
		Guid gameId,
		string userId,
		CancellationToken cancellationToken)
	{
		using var scope = CreateScope(gameId, userId);
		using var activity = PokerActivitySource.Source.StartActivity("realtime.broadcast");
		activity?.SetTag("hub", "notification");
		activity?.SetTag("event", eventName);
		activity?.SetTag("game.id", gameId);
		activity?.SetTag("user.id", userId);

		var stopwatch = Stopwatch.StartNew();
		try
		{
			await clientProxy.SendAsync(eventName, payload, cancellationToken);
			activity?.SetStatus(ActivityStatusCode.Ok);
			telemetry.RecordBroadcast("notification", eventName, "ok", stopwatch.Elapsed.TotalMilliseconds);
		}
		catch (Exception ex)
		{
			activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
			telemetry.RecordBroadcast("notification", eventName, "failed", stopwatch.Elapsed.TotalMilliseconds);
			logger.LogError(ex, "Error broadcasting {EventName} for game {GameId} to user {UserId}", eventName, gameId, userId);
			throw;
		}
	}

	private IDisposable? CreateScope(Guid gameId, string userId)
	{
		return logger.BeginScope(new Dictionary<string, object>
		{
			["GameId"] = gameId,
			["UserId"] = userId
		});
	}
}