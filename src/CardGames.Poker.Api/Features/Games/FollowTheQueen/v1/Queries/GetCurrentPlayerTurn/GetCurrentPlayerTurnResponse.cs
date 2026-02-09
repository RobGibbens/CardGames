using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Commands.DealHands;

namespace CardGames.Poker.Api.Features.Games.FollowTheQueen.v1.Queries.GetCurrentPlayerTurn;

/// <summary>
/// Response containing the current player's turn state within a specific Follow the Queen game,
/// including player information and available betting actions.
/// </summary>
public record GetCurrentPlayerTurnResponse(
	CurrentPlayerResponse Player,
	AvailableActionsResponse? AvailableActions = null,
	HandOddsResponse? HandOdds = null
);
