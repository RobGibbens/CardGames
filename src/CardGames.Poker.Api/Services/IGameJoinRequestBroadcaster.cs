using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services;

public interface IGameJoinRequestBroadcaster
{
	Task BroadcastJoinRequestReceivedAsync(string hostUserRoutingKey, GameJoinRequestReceivedDto payload, CancellationToken cancellationToken = default);
	Task BroadcastJoinRequestResolvedAsync(string playerUserRoutingKey, GameJoinRequestResolvedDto payload, CancellationToken cancellationToken = default);
}