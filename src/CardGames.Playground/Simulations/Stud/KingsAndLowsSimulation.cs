using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Dealers;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Playground.Simulations.Stud;

public class KingsAndLowsSimulation
{
    private FrenchDeckDealer _dealer;
    private IList<KingsAndLowsPlayer> _players = new List<KingsAndLowsPlayer>();
    private IReadOnlyCollection<Card> _deadCards = new List<Card>();
    private WildCardRules _wildCardRules = new WildCardRules(kingRequired: false);

    public KingsAndLowsSimulation WithPlayer(KingsAndLowsPlayer player)
    {
        _players.Add(player);
        return this;
    }

    public KingsAndLowsSimulation WithDeadCards(IReadOnlyCollection<Card> cards)
    {
        _deadCards = cards;
        return this;
    }

    public KingsAndLowsSimulation WithKingRequired(bool required = true)
    {
        _wildCardRules = new WildCardRules(kingRequired: required);
        return this;
    }

    public KingsAndLowsSimulationResult Simulate(int nrOfHands)
    {
        _dealer = FrenchDeckDealer.WithFullDeck();
        return Play(nrOfHands);
    }

    private KingsAndLowsSimulationResult Play(int nrOfHands)
    {
        var results = Enumerable
            .Range(1, nrOfHands)
            .Select(_ => PlayHand());

        return new KingsAndLowsSimulationResult(nrOfHands, results.ToList());
    }

    private IDictionary<string, KingsAndLowsHand> PlayHand()
    {
        _dealer.Shuffle();
        RemovePlayerCardsFromDeck();
        RemoveDeadCardsFromDeck();
        DealMissingHoleCards();
        DealMissingBoardCards();

        return _players.ToDictionary(
            player => player.Name,
            player => new KingsAndLowsHand(
                player.HoleCards.Take(2).ToList(),
                player.BoardCards.ToList(),
                player.HoleCards.Last(),
                _wildCardRules));
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

    private void DealMissingBoardCards()
        => _players.ForEach(player =>
            {
                var missingCards = 4 - player.GivenBoardCards.Count;
                player.DealtBoardCards = _dealer.DealCards(missingCards);
            });
}
