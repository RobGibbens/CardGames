namespace CardGames.Poker.Api.Features.Games.Baseball.v1.Queries.GetCurrentPlayerTurn;

public record GetCurrentPlayerTurnResponse(
	CurrentPlayerResponse Player,
	AvailableActionsResponse? AvailableActions = null,
	HandOddsResponse? HandOdds = null
);
