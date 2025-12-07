using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGame;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetGameMapper
{
	public static partial GetGameResponse ToResponse(this Game model);
	public static partial IQueryable<GetGameResponse> ProjectToResponse(this IQueryable<Game> query);

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();
}
