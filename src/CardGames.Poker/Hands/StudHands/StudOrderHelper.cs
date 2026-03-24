using System;
using System.Collections.Generic;
using System.Linq;

namespace CardGames.Poker.Hands.StudHands;

/// <summary>
/// Shared ordering helpers for stud-style games.
/// Keeps per-player card ordering and global face-up sequencing consistent across layers.
/// </summary>
public static class StudOrderHelper
{
    public static int GetStreetPhaseOrder(string? phase) => phase switch
    {
        "ThirdStreet" => 1,
        "FourthStreet" => 2,
        "FifthStreet" => 3,
        "SixthStreet" => 4,
        "SeventhStreet" => 5,
        _ => 999
    };

    public static int GetPlayerCardOrderKey(string? dealtAtPhase, bool isHoleCard, int dealOrder)
    {
        var phaseOrder = GetStreetPhaseOrder(dealtAtPhase);

        if (phaseOrder == 1)
        {
            return isHoleCard
                ? 1000 + dealOrder
                : 1100 + dealOrder;
        }

        return phaseOrder * 1000 + dealOrder;
    }

    public static IReadOnlyList<TCard> OrderPlayerCards<TCard>(
        IEnumerable<TCard> cards,
        Func<TCard, string?> phaseSelector,
        Func<TCard, bool> isHoleSelector,
        Func<TCard, int> dealOrderSelector)
    {
        return cards
            .OrderBy(card => GetPlayerCardOrderKey(
                phaseSelector(card),
                isHoleSelector(card),
                dealOrderSelector(card)))
            .ToList();
    }

    public static IReadOnlyList<TCard> OrderFaceUpCardsInGlobalDealOrder<TSeat, TCard>(
        IEnumerable<TSeat> seats,
        int dealerSeatIndex,
        Func<TSeat, int> seatIndexSelector,
        Func<TSeat, IEnumerable<TCard>> orderedCardsSelector,
        Func<TCard, bool> isFaceUpSelector)
    {
        var visibleCardsBySeat = seats
            .Select(seat => new
            {
                SeatIndex = seatIndexSelector(seat),
                Cards = orderedCardsSelector(seat)
                    .Where(isFaceUpSelector)
                    .ToList()
            })
            .Where(seat => seat.Cards.Count > 0)
            .OrderBy(seat => GetRotationOrder(seat.SeatIndex, dealerSeatIndex))
            .ToList();

        if (visibleCardsBySeat.Count == 0)
        {
            return [];
        }

        var result = new List<TCard>();
        var maxVisibleCards = visibleCardsBySeat.Max(seat => seat.Cards.Count);

        for (var visibleIndex = 0; visibleIndex < maxVisibleCards; visibleIndex++)
        {
            foreach (var seat in visibleCardsBySeat)
            {
                if (seat.Cards.Count <= visibleIndex)
                {
                    continue;
                }

                result.Add(seat.Cards[visibleIndex]);
            }
        }

        return result;
    }

    public static int GetRotationOrder(int seatIndex, int dealerSeatIndex)
        => seatIndex > dealerSeatIndex ? seatIndex : seatIndex + 1000;
}