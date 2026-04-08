using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Infrastructure.Fakes;

public class FakeGameJoinRequestBroadcaster : IGameJoinRequestBroadcaster
{
	public List<GameJoinRequestReceivedDto> ReceivedNotifications { get; } = [];
	public List<GameJoinRequestResolvedDto> ResolvedNotifications { get; } = [];

	public Task BroadcastJoinRequestReceivedAsync(string hostUserRoutingKey, GameJoinRequestReceivedDto payload, CancellationToken cancellationToken = default)
	{
		ReceivedNotifications.Add(payload);
		return Task.CompletedTask;
	}

	public Task BroadcastJoinRequestResolvedAsync(string playerUserRoutingKey, GameJoinRequestResolvedDto payload, CancellationToken cancellationToken = default)
	{
		ResolvedNotifications.Add(payload);
		return Task.CompletedTask;
	}
}