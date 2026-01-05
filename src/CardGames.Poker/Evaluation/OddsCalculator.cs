using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Hands;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.HandTypes;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Hands.WildCards;

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Calculates live poker hand odds using Monte Carlo simulation.
/// This class evaluates the probability of ending with each hand type
/// for a player at any point during a poker hand.
/// </summary>
public static class OddsCalculator
{
    /// <summary>
    /// Default number of simulations to run for Monte Carlo calculation.
    /// </summary>
    public const int DefaultSimulations = 1000;

    /// <summary>
    /// Result of an odds calculation containing hand type probabilities.
    /// </summary>
    public sealed record OddsResult
    {
        /// <summary>
        /// Probability of ending with each hand type (e.g., Pair, Flush, Full House).
        /// </summary>
        public required IReadOnlyDictionary<HandType, decimal> HandTypeProbabilities { get; init; }
    }

    /// <summary>
    /// Calculates odds for a Texas Hold'em hand.
    /// </summary>
    public static OddsResult CalculateHoldemOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> communityCards,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var dealer = FrenchDeckDealer.WithFullDeck();

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            var knownCards = heroHoleCards.Concat(communityCards).Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            var remainingCommunityCount = 5 - communityCards.Count;
            var simulatedCommunity = communityCards.ToList();
            for (int j = 0; j < remainingCommunityCount; j++)
            {
                simulatedCommunity.Add(dealer.DealCard());
            }

            var heroHand = new HoldemHand(heroHoleCards, simulatedCommunity);
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    /// <summary>
    /// Calculates odds for a Twos, Jacks, Man with the Axe five-card draw hand.
    /// Wild cards: all 2s, all Jacks, and the King of Diamonds.
    /// </summary>
    /// <param name="cardsToKeep">The cards the player is keeping (not discarding).</param>
    /// <param name="deadCards">Cards that are known to be unavailable (e.g., seen by other players).</param>
    /// <param name="simulations">Number of Monte Carlo simulations to run.</param>
    /// <returns>Probabilities for each possible hand type after the draw.</returns>
    public static OddsResult CalculateTwosJacksManWithTheAxeDrawOdds(
        IReadOnlyCollection<Card> cardsToKeep,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var cardsNeeded = 5 - cardsToKeep.Count;

        if (cardsNeeded < 0)
        {
            throw new ArgumentException($"Expected at most 5 cards to keep, but got {cardsToKeep.Count}");
        }

        // If no cards need to be drawn, just evaluate the current hand once.
        if (cardsNeeded == 0)
        {
            var heroHand = new TwosJacksManWithTheAxeDrawHand(cardsToKeep.ToList());
            handTypeCounts[heroHand.Type] = simulations;
            return CreateResult(handTypeCounts, simulations);
        }

        var dealer = FrenchDeckDealer.WithFullDeck();

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();

            var knownCards = cardsToKeep.Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            var finalHand = cardsToKeep.ToList();
            for (int j = 0; j < cardsNeeded; j++)
            {
                finalHand.Add(dealer.DealCard());
            }

            var heroHand = new TwosJacksManWithTheAxeDrawHand(finalHand);
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    /// <summary>
    /// Calculates odds for a Kings and Lows five-card draw hand.
    /// Wild cards: all Kings and the lowest card(s) in the hand.
    /// </summary>
    /// <param name="cardsToKeep">The cards the player is keeping (not discarding).</param>
    /// <param name="deadCards">Cards that are known to be unavailable (e.g., seen by other players).</param>
    /// <param name="simulations">Number of Monte Carlo simulations to run.</param>
    /// <returns>Probabilities for each possible hand type after the draw.</returns>
    public static OddsResult CalculateKingsAndLowsDrawOdds(
        IReadOnlyCollection<Card> cardsToKeep,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var cardsNeeded = 5 - cardsToKeep.Count;

        if (cardsNeeded < 0)
        {
            throw new ArgumentException($"Expected at most 5 cards to keep, but got {cardsToKeep.Count}");
        }

        // If no cards need to be drawn, just evaluate the current hand once.
        if (cardsNeeded == 0)
        {
            var heroHand = new KingsAndLowsDrawHand(cardsToKeep.ToList());
            handTypeCounts[heroHand.Type] = simulations;
            return CreateResult(handTypeCounts, simulations);
        }

        var dealer = FrenchDeckDealer.WithFullDeck();

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();

            var knownCards = cardsToKeep.Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            var finalHand = cardsToKeep.ToList();
            for (int j = 0; j < cardsNeeded; j++)
            {
                finalHand.Add(dealer.DealCard());
            }

            var heroHand = new KingsAndLowsDrawHand(finalHand);
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    /// <summary>
    /// Calculates odds for a Seven Card Stud hand.
    /// </summary>
    public static OddsResult CalculateStudOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> heroBoardCards,
        int totalCardsPerPlayer = 7,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var dealer = FrenchDeckDealer.WithFullDeck();
        var heroTotalCards = heroHoleCards.Count + heroBoardCards.Count;
        var heroCardsNeeded = totalCardsPerPlayer - heroTotalCards;

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            var allKnownCards = heroHoleCards.Concat(heroBoardCards).Concat(deadCards).Distinct();
            foreach (var card in allKnownCards)
            {
                dealer.DealSpecific(card);
            }

            var heroCards = heroHoleCards.Concat(heroBoardCards).ToList();
            for (int j = 0; j < heroCardsNeeded; j++)
            {
                heroCards.Add(dealer.DealCard());
            }

            var heroHand = CreateStudHand(heroCards);
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    /// <summary>
    /// Calculates odds for a Baseball hand (3s and 9s are wild).
    /// </summary>
    public static OddsResult CalculateBaseballOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> heroBoardCards,
        int totalCardsPerPlayer = 7,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var dealer = FrenchDeckDealer.WithFullDeck();
        var heroTotalCards = heroHoleCards.Count + heroBoardCards.Count;
        var heroCardsNeeded = totalCardsPerPlayer - heroTotalCards;

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            var allKnownCards = heroHoleCards.Concat(heroBoardCards).Concat(deadCards).Distinct();
            foreach (var card in allKnownCards)
            {
                dealer.DealSpecific(card);
            }

            var holeCards = heroHoleCards.ToList();
            var boardCards = heroBoardCards.ToList();
            var downCards = new List<Card>();
            
            for (int j = 0; j < heroCardsNeeded; j++)
            {
                downCards.Add(dealer.DealCard());
            }

            var heroHand = new BaseballHand(holeCards, boardCards, downCards);
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    /// <summary>
    /// Calculates odds for a Follow The Queen hand.
    /// Queens are always wild, and the card following the last face-up Queen is also wild.
    /// </summary>
    public static OddsResult CalculateFollowTheQueenOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> heroBoardCards,
        IReadOnlyCollection<Card> faceUpCardsInOrder,
        int totalCardsPerPlayer = 7,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var dealer = FrenchDeckDealer.WithFullDeck();
        var heroTotalCards = heroHoleCards.Count + heroBoardCards.Count;
        var heroCardsNeeded = totalCardsPerPlayer - heroTotalCards;

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            var allKnownCards = heroHoleCards.Concat(heroBoardCards).Concat(deadCards).Distinct();
            foreach (var card in allKnownCards)
            {
                dealer.DealSpecific(card);
            }

            var holeCards = heroHoleCards.ToList();
            var boardCards = heroBoardCards.ToList();
            
            var additionalCards = new List<Card>();
            for (int j = 0; j < heroCardsNeeded; j++)
            {
                additionalCards.Add(dealer.DealCard());
            }

            var downCard = additionalCards.Any() ? additionalCards.Last() : holeCards.Last();
            
            if (additionalCards.Count > 1)
            {
                boardCards.AddRange(additionalCards.Take(additionalCards.Count - 1));
            }

            var heroHand = new FollowTheQueenHand(holeCards, boardCards, downCard, faceUpCardsInOrder);
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    /// <summary>
    /// Calculates odds for a Kings and Lows hand.
    /// Kings are always wild, and the lowest card in the hand is also wild.
    /// This takes into account that drawing new cards could change which card is lowest.
    /// </summary>
    public static OddsResult CalculateKingsAndLowsOdds(
        IReadOnlyCollection<Card> heroCards,
        bool kingRequired = false,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var dealer = FrenchDeckDealer.WithFullDeck();
        var wildCardRules = new WildCardRules(kingRequired);

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            var knownCards = heroCards.Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            var cardList = heroCards.ToList();
            
            while (cardList.Count < 7)
            {
                cardList.Add(dealer.DealCard());
            }

            var heroHand = new KingsAndLowsHand(
                cardList.Take(2).ToList(),
                cardList.Skip(2).Take(4).ToList(),
                cardList.Last(),
                wildCardRules);
            
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    /// <summary>
    /// Calculates odds for a Five Card Draw hand.
    /// When cardsToKeep contains fewer than 5 cards, simulates drawing replacement cards
    /// to calculate the probability of ending with each hand type after the draw.
    /// </summary>
    /// <param name="cardsToKeep">The cards the player is keeping (not discarding).</param>
    /// <param name="deadCards">Cards that are known to be unavailable (e.g., seen by other players).</param>
    /// <param name="simulations">Number of Monte Carlo simulations to run.</param>
    /// <returns>Probabilities for each possible hand type after the draw.</returns>
    public static OddsResult CalculateDrawOdds(
        IReadOnlyCollection<Card> cardsToKeep,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var cardsNeeded = 5 - cardsToKeep.Count;

        // If no cards need to be drawn, just evaluate the hand once
        if (cardsNeeded == 0)
        {
            var heroHand = new DrawHand(cardsToKeep.ToList());
            handTypeCounts[heroHand.Type] = simulations;
            return CreateResult(handTypeCounts, simulations);
        }

        var dealer = FrenchDeckDealer.WithFullDeck();

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();

            // Remove known cards from the deck
            var knownCards = cardsToKeep.Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            // Build the final hand: kept cards + drawn replacements
            var finalHand = cardsToKeep.ToList();
            for (int j = 0; j < cardsNeeded; j++)
            {
                finalHand.Add(dealer.DealCard());
            }

            var heroHand = new DrawHand(finalHand);
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    /// <summary>
    /// Calculates odds for an Omaha hand.
    /// </summary>
    public static OddsResult CalculateOmahaOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> communityCards,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        var handTypeCounts = InitializeHandTypeCounts();
        var dealer = FrenchDeckDealer.WithFullDeck();

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            var knownCards = heroHoleCards.Concat(communityCards).Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            var remainingCommunityCount = 5 - communityCards.Count;
            var simulatedCommunity = communityCards.ToList();
            for (int j = 0; j < remainingCommunityCount; j++)
            {
                simulatedCommunity.Add(dealer.DealCard());
            }

            var heroHand = new OmahaHand(heroHoleCards, simulatedCommunity);
            handTypeCounts[heroHand.Type]++;
        }

        return CreateResult(handTypeCounts, simulations);
    }

    private static Dictionary<HandType, int> InitializeHandTypeCounts()
    {
        return Enum.GetValues<HandType>()
            .Where(t => t != HandType.Incomplete)
            .ToDictionary(t => t, _ => 0);
    }

    private static HandBase CreateStudHand(IReadOnlyCollection<Card> cards)
    {
        if (cards.Count < 7)
        {
            throw new ArgumentException($"Expected at least 7 cards for stud hand, but got {cards.Count}");
        }
        
        var cardList = cards.ToList();
        return new SevenCardStudHand(
            cardList.Take(2).ToList(),
            cardList.Skip(2).Take(4).ToList(),
            cardList.Last());
    }

    private static OddsResult CreateResult(Dictionary<HandType, int> handTypeCounts, int simulations)
    {
        var handTypeProbabilities = handTypeCounts
            .Where(kvp => kvp.Value > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (decimal)kvp.Value / simulations);

        return new OddsResult
        {
            HandTypeProbabilities = handTypeProbabilities
        };
    }
}
