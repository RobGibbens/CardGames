namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetRabbitHunt;

public enum GetRabbitHuntErrorCode
{
    NotAuthenticated,
    GameNotFound,
    NotSeated,
    UnsupportedGameType,
    RabbitHuntNotAvailable
}

public sealed record GetRabbitHuntError(GetRabbitHuntErrorCode Code, string Message);