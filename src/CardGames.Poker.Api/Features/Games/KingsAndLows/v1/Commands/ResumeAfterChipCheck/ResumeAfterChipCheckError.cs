namespace CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.ResumeAfterChipCheck;

public class ResumeAfterChipCheckError
{
	public required string Message { get; init; }
	public required ResumeAfterChipCheckErrorCode Code { get; init; }
}

public enum ResumeAfterChipCheckErrorCode
{
	GameNotFound,
	GameNotPaused,
	PlayersStillShort
}
