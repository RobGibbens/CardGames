using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

public class FakeGameStateBroadcaster : IGameStateBroadcaster
{
	public List<TableToastNotificationDto> ToastNotifications { get; } = [];

	public Task<TableStatePublicDto?> BroadcastGameStateAsync(Guid gameId, CancellationToken cancellationToken = default)
	{
		return Task.FromResult<TableStatePublicDto?>(null);
	}

	public Task BroadcastGameStateToUserAsync(Guid gameId, string userId, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	public Task BroadcastPlayerJoinedAsync(Guid gameId, string playerName, int seatIndex, bool canPlayCurrentHand,
		Guid playerId, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	public Task BroadcastTableToastAsync(TableToastNotificationDto notification,
		CancellationToken cancellationToken = default)
	{
		ToastNotifications.Add(notification);
		return Task.CompletedTask;
	}

	public Task BroadcastTableSettingsUpdatedAsync(TableSettingsUpdatedDto notification,
		CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	public Task BroadcastOddsVisibilityUpdatedAsync(OddsVisibilityUpdatedDto notification,
		CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}

	public Task BroadcastPlayerActionAsync(Guid gameId, int seatIndex, string? playerName, string actionDescription,
		int handNumber, ulong stateVersion, CancellationToken cancellationToken = default)
	{
		return Task.CompletedTask;
	}
}