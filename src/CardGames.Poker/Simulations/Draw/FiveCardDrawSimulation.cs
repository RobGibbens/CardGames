using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Dealers;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.Poker.Simulations.Draw;

/// <summary>
/// Five Card Draw simulation: a classic poker variant where each player receives five cards face down.
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
public class FiveCardDrawSimulation
{
    private FrenchDeckDealer _dealer;
    private readonly IList<FiveCardDrawPlayer> _players = new List<FiveCardDrawPlayer>();
    private IReadOnlyCollection<Card> _deadCards = new List<Card>();

    public FiveCardDrawSimulation WithPlayer(FiveCardDrawPlayer player)
    {
        _players.Add(player);
        return this;
    }

    public FiveCardDrawSimulation WithPlayer(string name, IReadOnlyCollection<Card> cards)
        => WithPlayer(new FiveCardDrawPlayer(name).WithCards(cards));

    public FiveCardDrawSimulation WithDeadCards(IReadOnlyCollection<Card> cards)
    {
        _deadCards = cards;
        return this;
    }

    public FiveCardDrawSimulationResult Simulate(int nrOfHands)
    {
        _dealer = FrenchDeckDealer.WithFullDeck();
        return Play(nrOfHands);
    }

    private FiveCardDrawSimulationResult Play(int nrOfHands)
    {
        var results = Enumerable
            .Range(1, nrOfHands)
            .Select(_ => PlayHand());

        return new FiveCardDrawSimulationResult(nrOfHands, results.ToList());
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
