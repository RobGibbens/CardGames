using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetGames;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetGamesMapper
{
	public static partial GetGamesResponse ToResponse(this Game model);
	public static partial IQueryable<GetGamesResponse> ProjectToResponse(this IQueryable<Game> query);

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();
}