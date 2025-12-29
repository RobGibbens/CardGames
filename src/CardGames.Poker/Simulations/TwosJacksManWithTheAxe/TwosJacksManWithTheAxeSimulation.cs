using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Dealers;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.Poker.Simulations.TwosJacksManWithTheAxe;

/// <summary>
/// Twos, Jacks, Man with the Axe simulation: a classic poker variant where each player receives five cards face down.
/// 
/// Basic rules:
/// - Each player receives five cards face down
/// - A round of betting occurs
/// - Players can discard and replace cards (draw round)
/// - Another round of betting occurs
/// - Showdown: best five-card poker hand wins
/// 
/// Note: This simulation focuses on the showdown evaluation, 
/// dealing complete five-card hands to each player.
/// </summary>
public class TwosJacksManWithTheAxeSimulation
{
    private FrenchDeckDealer _dealer;
    private readonly IList<TwosJacksManWithTheAxePlayer> _players = new List<TwosJacksManWithTheAxePlayer>();
    private IReadOnlyCollection<Card> _deadCards = new List<Card>();

    public TwosJacksManWithTheAxeSimulation WithPlayer(TwosJacksManWithTheAxePlayer player)
    {
        _players.Add(player);
        return this;
    }

    public TwosJacksManWithTheAxeSimulation WithPlayer(string name, IReadOnlyCollection<Card> cards)
        => WithPlayer(new TwosJacksManWithTheAxePlayer(name).WithCards(cards));

    public TwosJacksManWithTheAxeSimulation WithDeadCards(IReadOnlyCollection<Card> cards)
    {
        _deadCards = cards;
        return this;
    }

    public TwosJacksManWithTheAxeSimulationResult Simulate(int nrOfHands)
    {
        _dealer = FrenchDeckDealer.WithFullDeck();
        return Play(nrOfHands);
    }

    private TwosJacksManWithTheAxeSimulationResult Play(int nrOfHands)
    {
        var results = Enumerable
            .Range(1, nrOfHands)
            .Select(_ => PlayHand());

        return new TwosJacksManWithTheAxeSimulationResult(nrOfHands, results.ToList());
    }

    private IDictionary<string, DrawHand> PlayHand()
    {
        _dealer.Shuffle();
        RemovePlayerCardsFromDeck();
        RemoveDeadCardsFromDeck();
        DealMissingCards();

        return _players.ToDictionary(
            player => player.Name,
            player => new DrawHand(player.Cards.ToList()));
    }

    private void RemoveDeadCardsFromDeck()
        => _deadCards.ForEach(card => _dealer.DealSpecific(card));

    private void RemovePlayerCardsFromDeck()
        => _players
            .SelectMany(player => player.GivenCards)
            .ForEach(card => _dealer.DealSpecific(card));

    private void DealMissingCards()
        => _players.ForEach(player =>
            {
                var missingCards = 5 - player.GivenCards.Count;
                player.DealtCards = _dealer.DealCards(missingCards);
            });
}
