using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Dealers;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Playground.Simulations.Stud;

/// <summary>
/// Follow the Queen simulation: a seven card stud variant with wild cards.
/// - Queens are always wild
/// - When a Queen is dealt face up, the next face-up card's rank becomes wild
/// - If another Queen is dealt later, the new following card replaces the previous wild rank
/// - If a Queen is the last face-up card, only Queens are wild
/// 
/// Deal structure: two down, four up (streets), one down
/// </summary>
public class FollowTheQueenSimulation
{
    private FrenchDeckDealer _dealer;
    private IList<FollowTheQueenPlayer> _players = new List<FollowTheQueenPlayer>();
    private IReadOnlyCollection<Card> _deadCards = new List<Card>();

    public FollowTheQueenSimulation WithPlayer(FollowTheQueenPlayer player)
    {
        _players.Add(player);
        return this;
    }

    public FollowTheQueenSimulation WithDeadCards(IReadOnlyCollection<Card> cards)
    {
        _deadCards = cards;
        return this;
    }

    public FollowTheQueenSimulationResult Simulate(int nrOfHands)
    {
        _dealer = FrenchDeckDealer.WithFullDeck();
        return Play(nrOfHands);
    }

    private FollowTheQueenSimulationResult Play(int nrOfHands)
    {
        var results = Enumerable
            .Range(1, nrOfHands)
            .Select(_ => PlayHand());

        return new FollowTheQueenSimulationResult(nrOfHands, results.ToList());
    }

    private IDictionary<string, FollowTheQueenHand> PlayHand()
    {
        _dealer.Shuffle();
        RemovePlayerCardsFromDeck();
        RemoveDeadCardsFromDeck();
        DealMissingHoleCards();
        var faceUpCardsInOrder = DealMissingBoardCards();

        return _players.ToDictionary(
            player => player.Name,
            player => new FollowTheQueenHand(
                player.HoleCards.Take(2).ToList(),
                player.BoardCards.ToList(),
                player.HoleCards.Last(),
                faceUpCardsInOrder));
    }

    private void RemoveDeadCardsFromDeck()
        => _deadCards.ForEach(card => _dealer.DealSpecific(card));

    private void RemovePlayerCardsFromDeck()
        => _players
            .SelectMany(player => player.Cards)
            .ForEach(card => _dealer.DealSpecific(card));

    private void DealMissingHoleCards()
        => _players.ForEach(player =>
            {
                var missingCards = 3 - player.GivenHoleCards.Count;
                player.DealtHoleCards = _dealer.DealCards(missingCards);
            });

    /// <summary>
    /// Deals face-up cards in proper order for Follow the Queen.
    /// In 7-card stud, face-up cards are dealt round-robin to all players:
    /// - Third Street (door card): one card to each player
    /// - Fourth Street: one card to each player
    /// - Fifth Street: one card to each player
    /// - Sixth Street: one card to each player
    /// This order is important for determining which card follows a Queen.
    /// </summary>
    /// <returns>All face-up cards in the order they were dealt.</returns>
    private IReadOnlyCollection<Card> DealMissingBoardCards()
    {
        var faceUpCardsInOrder = new List<Card>();
        
        // Initialize dealt board cards lists for each player
        foreach (var player in _players)
        {
            player.DealtBoardCards = new List<Card>();
        }
        
        // Deal board cards street by street (4 streets total for face-up cards)
        // Cards are dealt round-robin: each player gets one card per street
        for (int street = 0; street < 4; street++)
        {
            foreach (var player in _players)
            {
                // Check if player already has a given card for this street
                if (player.GivenBoardCards.Count > street)
                {
                    // Use the given card for this street
                    faceUpCardsInOrder.Add(player.GivenBoardCards.ElementAt(street));
                }
                else
                {
                    // Deal a new card for this street
                    var card = _dealer.DealCard();
                    ((List<Card>)player.DealtBoardCards).Add(card);
                    faceUpCardsInOrder.Add(card);
                }
            }
        }

        return faceUpCardsInOrder;
    }
}
