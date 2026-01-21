namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.ToggleSitOut;

public record ToggleSitOutSuccessful(
	Guid GameId,
	bool IsSittingOut,
	string Message);
