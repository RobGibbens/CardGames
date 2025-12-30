using MediatR;

namespace CardGames.Poker.Api.Features.Games.TwosJacksManWithTheAxe.v1.Queries.GetCurrentBettingRound;

/// <summary>
/// Query to retrieve the current betting round for a specific game.
/// </summary>
public record GetCurrentBettingRoundQuery(Guid GameId) : IRequest<GetCurrentBettingRoundResponse?>
{
	public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetCurrentBettingRoundQuery)}:{GameId}";
}
