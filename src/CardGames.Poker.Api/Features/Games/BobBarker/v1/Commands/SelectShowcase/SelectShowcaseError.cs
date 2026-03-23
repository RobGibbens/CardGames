namespace CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;

public record SelectShowcaseError
{
    public required string Message { get; init; }

    public required SelectShowcaseErrorCode Code { get; init; }
}

public enum SelectShowcaseErrorCode
{
    GameNotFound,
    NotInShowcasePhase,
    NotPlayerTurn,
    NoEligiblePlayers,
    InvalidCardIndex,
    AlreadySelected,
    InsufficientCards
}