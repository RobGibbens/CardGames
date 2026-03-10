namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleOddsVisibility;

/// <summary>
/// Request model for toggling table odds visibility.
/// </summary>
/// <param name="AreOddsVisibleToAllPlayers">Whether odds should be visible to all players.</param>
public sealed record ToggleOddsVisibilityRequest(bool AreOddsVisibleToAllPlayers);
