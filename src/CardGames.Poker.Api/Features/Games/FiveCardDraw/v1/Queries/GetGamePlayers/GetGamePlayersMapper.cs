using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGamePlayers;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetGamePlayersMapper
{
	[MapProperty(nameof(GamePlayer.Player) + "." + nameof(Player.Name), nameof(GetGamePlayersResponse.PlayerName))]
	public static partial GetGamePlayersResponse ToResponse(this GamePlayer model);

	public static partial IQueryable<GetGamePlayersResponse> ProjectToResponse(this IQueryable<GamePlayer> query);

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();
}
