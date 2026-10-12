using Microsoft.AspNetCore.SignalR.Client;

namespace CardGames.Poker.Web.Services;

/// <summary>
///     Decides when a game-hub circuit may open a connection, and whether a callback still belongs to that connection.
/// </summary>
public static class GameHubConnectionPolicy
{
	public static bool ShouldBuildNewConnection(HubConnectionState? state)
		=> state is null or HubConnectionState.Disconnected;

	public static bool IsCurrent(int handlerGeneration, int currentGeneration)
		=> handlerGeneration == currentGeneration;
}
