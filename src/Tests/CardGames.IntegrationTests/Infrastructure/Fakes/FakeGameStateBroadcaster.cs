using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

public class FakeGameStateBroadcaster : IGameStateBroadcaster
{
    public Task BroadcastGameStateAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastGameStateToUserAsync(Guid gameId, string userId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastPlayerJoinedAsync(Guid gameId, string playerName, int seatIndex, bool canPlayCurrentHand, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastTableSettingsUpdatedAsync(TableSettingsUpdatedDto notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastPlayerActionAsync(Guid gameId, int seatIndex, string? playerName, string actionDescription, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
