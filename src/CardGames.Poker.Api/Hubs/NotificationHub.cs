using CardGames.Poker.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Hubs;

[Authorize(AuthenticationSchemes = HeaderAuthenticationHandler.SchemeName)]
public sealed class NotificationHub(ILogger<NotificationHub> logger) : Hub
{
	public override async Task OnConnectedAsync()
	{
		logger.LogInformation("User {UserId} connected to notification hub with connection {ConnectionId}", Context.UserIdentifier ?? "unknown", Context.ConnectionId);
		await base.OnConnectedAsync();
	}

	public override async Task OnDisconnectedAsync(Exception? exception)
	{
		if (exception is not null)
		{
			logger.LogWarning(exception, "Notification hub connection {ConnectionId} closed with error for user {UserId}", Context.ConnectionId, Context.UserIdentifier ?? "unknown");
		}
		else
		{
			logger.LogInformation("Notification hub connection {ConnectionId} closed for user {UserId}", Context.ConnectionId, Context.UserIdentifier ?? "unknown");
		}

		await base.OnDisconnectedAsync(exception);
	}
}