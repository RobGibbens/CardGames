namespace CardGames.Poker.Api.Features.Games.InBetween.v1.Commands.AceChoice;

public class AceChoiceSuccessful
{
	public required Guid GameId { get; init; }
	public required Guid PlayerId { get; init; }
	public required bool AceIsHigh { get; init; }
	public string? NextSubPhase { get; init; }
}
