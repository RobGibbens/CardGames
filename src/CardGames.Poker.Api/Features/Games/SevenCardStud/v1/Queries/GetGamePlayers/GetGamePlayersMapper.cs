using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;
using Riok.Mapperly.Abstractions;

namespace CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Queries.GetGamePlayers;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class GetGamePlayersMapper
{
       [MapProperty(nameof(GamePlayer.Player) + "." + nameof(Player.Name), nameof(GetGamePlayersResponse.PlayerName))]
       public static GetGamePlayersResponse ToResponse(
		   GamePlayer model,
		   int currentHandNumber,
		   string? playerFirstName,
		   string? playerAvatarUrl)
       {
               return new GetGamePlayersResponse(
                       model.Id,
                       model.GameId,
                       model.PlayerId,
                       model.Player.Name,
			playerFirstName,
			playerAvatarUrl,
                       model.SeatPosition,
                       model.ChipStack,
                       model.StartingChips,
                       model.CurrentBet,
                       model.TotalContributedThisHand,
                       model.HasFolded,
                       model.IsAllIn,
                       model.IsConnected,
                       model.IsSittingOut,
                       model.HasDrawnThisRound,
                       model.Status,
                       model.JoinedAt,
                       model.RowVersion.ToBase64String(),
                       MapCards(model.Cards, currentHandNumber)
               );
       }

       private static IReadOnlyList<DealtCard> MapCards(ICollection<GameCard> cards, int currentHandNumber) =>
               cards
                       .Where(c => !c.IsDiscarded && c.HandNumber == currentHandNumber)
                       .OrderBy(c => c.DealOrder)
                       .Select(c => new DealtCard
                       {
                               Suit = c.Suit,
                               Symbol = c.Symbol,
                               DealOrder = c.DealOrder
                       })
                       .ToList();
}
