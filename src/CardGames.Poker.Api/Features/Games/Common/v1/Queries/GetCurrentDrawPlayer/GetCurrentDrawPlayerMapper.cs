using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using Riok.Mapperly.Abstractions;
using CardGames.Poker.Api.Models;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Queries.GetCurrentDrawPlayer;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetCurrentDrawPlayerMapper
{
	[MapProperty(nameof(GamePlayer.Player) + "." + nameof(Player.Name), nameof(GetCurrentDrawPlayerResponse.PlayerName))]
	[MapProperty(nameof(GamePlayer.Cards), nameof(GetCurrentDrawPlayerResponse.Hand))]
	[MapperIgnoreSource(nameof(GamePlayer.Game))]
	[MapperIgnoreSource(nameof(GamePlayer.Player))]
	[MapperIgnoreSource(nameof(GamePlayer.Cards))]
	[MapperIgnoreSource(nameof(GamePlayer.DropOrStayDecision))]
	[MapperIgnoreSource(nameof(GamePlayer.JoinedAtHandNumber))]
	[MapperIgnoreSource(nameof(GamePlayer.LeftAtHandNumber))]
	[MapperIgnoreSource(nameof(GamePlayer.FinalChipCount))]
	[MapperIgnoreSource(nameof(GamePlayer.VariantState))]
	[MapperIgnoreSource(nameof(GamePlayer.LeftAt))]
	[MapperIgnoreSource(nameof(GamePlayer.PotContributions))]
	[MapperIgnoreSource(nameof(GamePlayer.BettingActions))]
	public static partial GetCurrentDrawPlayerResponse ToResponse(this GamePlayer model);

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
