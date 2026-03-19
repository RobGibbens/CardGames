using MediatR;

namespace CardGames.Poker.Api.Features.Games.Generic.v1.Queries.GetGeneratedTableName;

public record GetGeneratedTableNameQuery(string? GameType = null) : IRequest<GetGeneratedTableNameResponse>
{
    public string CacheKey => $"{Feature.Name}:{Feature.Version}:{nameof(GetGeneratedTableNameQuery)}";
}