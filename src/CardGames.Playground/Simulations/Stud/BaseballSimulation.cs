using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Dealers;
using CardGames.Core.Extensions;
using CardGames.Core.French.Cards;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Playground.Simulations.Stud;

/// <summary>
/// Baseball simulation: a seven card stud variant with wild cards and extra cards.
/// - All 3s and 9s are wild
/// - When a player receives a 4 face up, they get an extra face up card
/// - Deal: two down, four up, one down (standard seven card stud)
/// </summary>
public class BaseballSimulation
{
    private FrenchDeckDealer _dealer;
    private IList<BaseballPlayer> _players = new List<BaseballPlayer>();
    private IReadOnlyCollection<Card> _deadCards = new List<Card>();

    public BaseballSimulation WithPlayer(BaseballPlayer player)
    {
        _players.Add(player);
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
        DealMissingBoardCards();

        return _players.ToDictionary(
            player => player.Name,
            player => new BaseballHand(
                player.HoleCards.Take(2).ToList(),
                player.BoardCards.ToList(),
                player.HoleCards.Skip(2).ToList()));
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
    {
        _players.ForEach(player =>
        {
            var missingCards = 4 - player.GivenBoardCards.Count;
            var dealtBoardCards = new List<Card>();
            var extraCards = new List<Card>();
            
            // Deal the missing board cards, checking for 4s which grant extra cards
            for (int i = 0; i < missingCards; i++)
            {
                var card = _dealer.DealCard();
                dealtBoardCards.Add(card);
                
                // If a 4 is dealt face up, deal an extra card
                if (card.Symbol == Symbol.Four)
                {
                    var extraCard = _dealer.DealCard();
                    extraCards.Add(extraCard);
                    
                    // The extra card might also be a 4, granting another extra card
                    while (extraCard.Symbol == Symbol.Four)
                    {
                        extraCard = _dealer.DealCard();
                        extraCards.Add(extraCard);
                    }
                }
            }
            
            // Also check given board cards for 4s that weren't yet dealt extra cards
            var givenFours = player.GivenBoardCards.Count(c => c.Symbol == Symbol.Four);
            for (int i = 0; i < givenFours; i++)
            {
                var extraCard = _dealer.DealCard();
                extraCards.Add(extraCard);
                
                while (extraCard.Symbol == Symbol.Four)
                {
                    extraCard = _dealer.DealCard();
                    extraCards.Add(extraCard);
                }
            }
            
            player.DealtBoardCards = dealtBoardCards;
            player.ExtraBoardCards = extraCards;
        });
    }
}
