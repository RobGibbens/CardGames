using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Queries.GetGamePlayers;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetGamePlayersMapper
{
	[MapProperty(nameof(GamePlayer.Player) + "." + nameof(Player.Name), nameof(GetGamePlayersResponse.PlayerName))]
	[MapProperty(nameof(GamePlayer.Cards), nameof(GetGamePlayersResponse.Hand))]
	public static partial GetGamePlayersResponse ToResponse(this GamePlayer model);

	public static partial IQueryable<GetGamePlayersResponse> ProjectToResponse(this IQueryable<GamePlayer> query);

	private static string MapRowVersion(byte[] rowVersion) => rowVersion.ToBase64String();

	private static IReadOnlyList<DealtCard> MapCards(ICollection<GameCard> cards) =>
		cards
			.Where(c => !c.IsDiscarded)
			.OrderBy(c => c.DealOrder)
			.Select(c => new DealtCard
			{
				Suit = c.Suit,
				Symbol = c.Symbol,
				DealOrder = c.DealOrder
			})
			.ToList();
}
