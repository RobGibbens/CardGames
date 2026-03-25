namespace CardGames.Poker.Api.Features.Games.Tollbooth.v1.Commands.CollectAntes;

public record CollectAntesError
{
	public required string Message { get; init; }
	public required CollectAntesErrorCode Code { get; init; }
}

public enum CollectAntesErrorCode
{
	GameNotFound,
	InvalidGameState
}
