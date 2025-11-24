using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Core.Extensions;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Playground.Simulations.Baseball;

/// <summary>
/// Simulates Baseball poker hands.
/// Baseball is a seven card stud variant where:
/// - 3s and 9s are wild cards
/// - 4s dealt face-up give the player an extra face-up card
/// - Five of a Kind is possible and is the highest hand
/// </summary>
public class BaseballSimulation
{
    private FrenchDeckDealer _dealer;
    private IList<BaseballPlayer> _players = new List<BaseballPlayer>();
    private IReadOnlyCollection<Card> _deadCards = new List<Card>();

    /// <summary>
    /// Maximum number of players recommended to avoid running out of cards.
    /// </summary>
    public const int MaxRecommendedPlayers = 6;

    public BaseballSimulation WithPlayer(BaseballPlayer player)
    {
        _players.Add(player);
        return this;
    }

    public BaseballSimulation WithPlayer(string name)
    {
        _players.Add(new BaseballPlayer(name));
        return this;
    }

    public BaseballSimulation WithDeadCards(IReadOnlyCollection<Card> cards)
    {
        _deadCards = cards;
        return this;
    }

    public BaseballSimulationResult Simulate(int nrOfHands)
    {
        _dealer = FrenchDeckDealer.WithFullDeck();
        return Play(nrOfHands);
    }

    private BaseballSimulationResult Play(int nrOfHands)
    {
        var results = Enumerable
            .Range(1, nrOfHands)
            .Select(_ => PlayHand());

        return new BaseballSimulationResult(nrOfHands, results.ToList());
    }

    private IDictionary<string, BaseballHand> PlayHand()
    {
        _dealer.Shuffle();
        RemovePlayerCardsFromDeck();
        RemoveDeadCardsFromDeck();
        DealMissingHoleCards();
        DealOpenCards();
        DealFinalDownCard();

        return _players.ToDictionary(
            player => player.Name,
            player => new BaseballHand(
                player.HoleCards.ToList(),
                player.OpenCards.ToList(),
                player.DownCard != null ? new[] { player.DownCard } : Array.Empty<Card>()));
    }

    private void RemoveDeadCardsFromDeck()
        => _deadCards.ForEach(card => _dealer.DealSpecific(card));

    private void RemovePlayerCardsFromDeck()
        => _players
            .SelectMany(player => player.Cards)
            .ForEach(card => _dealer.DealSpecific(card));

    /// <summary>
    /// Deals the initial 2 hole cards (face down) to each player.
    /// </summary>
    private void DealMissingHoleCards()
        => _players.ForEach(player =>
        {
            var missingCards = 2 - player.GivenHoleCards.Count;
            if (missingCards > 0)
            {
                player.DealtHoleCards = _dealer.DealCards(missingCards);
            }
        });

    /// <summary>
    /// Deals the 4 face-up cards to each player.
    /// If a 4 is dealt face-up, an extra card is dealt immediately.
    /// </summary>
    private void DealOpenCards()
    {
        const int standardOpenCards = 4;

        _players.ForEach(player =>
        {
            var givenOpenCount = player.GivenOpenCards.Count;
            var cardsToDeliver = standardOpenCards - givenOpenCount;
            
            if (cardsToDeliver <= 0)
            {
                // Check given open cards for 4s and deal bonus cards
                var foursInGiven = player.GivenOpenCards.Count(c => c.Symbol == Symbol.Four);
                if (foursInGiven > 0)
                {
                    player.DealtOpenCards = _dealer.DealCards(foursInGiven);
                }
                return;
            }

            var dealtCards = new List<Card>();
            for (int i = 0; i < cardsToDeliver; i++)
            {
                var card = _dealer.DealCard();
                dealtCards.Add(card);

                // If a 4 is dealt face-up, deal an extra card
                if (card.Symbol == Symbol.Four)
                {
                    var bonusCard = _dealer.DealCard();
                    dealtCards.Add(bonusCard);
                    
                    // The bonus card could also be a 4, which would give another bonus
                    while (dealtCards.Last().Symbol == Symbol.Four)
                    {
                        dealtCards.Add(_dealer.DealCard());
                    }
                }
            }

            player.DealtOpenCards = dealtCards;
        });
    }

    /// <summary>
    /// Deals the final down card to each player.
    /// </summary>
    private void DealFinalDownCard()
        => _players.ForEach(player =>
        {
            if (player.GivenDownCard == null)
            {
                player.DealtDownCard = _dealer.DealCard();
            }
        });
}
