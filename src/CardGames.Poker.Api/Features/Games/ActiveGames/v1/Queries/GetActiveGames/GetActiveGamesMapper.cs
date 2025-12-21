using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.ActiveGames.v1.Queries.GetActiveGames;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetActiveGamesMapper
{
	public static GetActiveGamesResponse ToResponse(this Game model)
	{
		return new GetActiveGamesResponse(
			model.Id,
			model.GameTypeId,
			model.GameType.Code,
			model.GameType.Name,
			null,
			model.GameType.Description,
			null,
			model.Name,
			model.CurrentPhase,
			model.Status,
			model.CreatedAt,
			MapRowVersion(model.RowVersion)
		);
	}

	public static IQueryable<GetActiveGamesResponse> ProjectToResponse(this IQueryable<Game> query)
	{
		return query.Select(model => ToResponse(model));
	}

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();
}
