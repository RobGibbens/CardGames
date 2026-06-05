namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
///     Response containing the current player's turn state within a specific game,
///     including player information and available betting actions.
/// </summary>
public record GetCurrentPlayerTurnResponse(
	CurrentPlayerResponse Player,
	AvailableActionsResponse? AvailableActions = null,
	HandOddsResponse? HandOdds = null
);