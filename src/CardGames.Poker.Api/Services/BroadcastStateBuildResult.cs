using CardGames.Contracts.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
/// Contains the complete broadcast state for a game, built in a single batch operation.
/// Returned by <see cref="ITableStateBuilder.BuildFullStateAsync"/> to eliminate N+1 queries.
/// </summary>
public sealed record BroadcastStateBuildResult(
    TableStatePublicDto PublicState,
    IReadOnlyDictionary<string, PrivateStateDto> PrivateStatesByUserId,
    IReadOnlyList<string> PlayerUserIds,
    ulong VersionNumber,
    int HandNumber,
    string Phase);
