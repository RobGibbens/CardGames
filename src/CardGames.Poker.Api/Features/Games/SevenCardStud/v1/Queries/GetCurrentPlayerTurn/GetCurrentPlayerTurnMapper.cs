using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetCurrentPlayerTurn;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetCurrentPlayerTurnMapper
{
	[MapProperty(nameof(GamePlayer.Player) + "." + nameof(Player.Name), nameof(CurrentPlayerResponse.PlayerName))]
	[MapProperty(nameof(GamePlayer.Cards), nameof(CurrentPlayerResponse.Hand))]
	[MapperIgnoreSource(nameof(GamePlayer.Game))]
	public static partial CurrentPlayerResponse ToCurrentPlayerResponse(this GamePlayer model);

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
