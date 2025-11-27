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

namespace CardGames.Poker.Evaluation;

/// <summary>
/// Calculates live poker hand odds using Monte Carlo simulation.
/// This class evaluates all possible hand types and win/tie/lose probabilities
/// for a player at any point during a poker hand.
/// </summary>
public static class OddsCalculator
{
    /// <summary>
    /// Default number of simulations to run for Monte Carlo calculation.
    /// </summary>
    public const int DefaultSimulations = 1000;

    /// <summary>
    /// Result of an odds calculation containing hand type probabilities and win/tie/lose percentages.
    /// </summary>
    public sealed record OddsResult
    {
        /// <summary>
        /// Probability of ending with each hand type (e.g., Pair, Flush, Full House).
        /// </summary>
        public required IReadOnlyDictionary<HandType, decimal> HandTypeProbabilities { get; init; }

        /// <summary>
        /// Probability of winning the hand outright.
        /// </summary>
        public decimal WinProbability { get; init; }

        /// <summary>
        /// Probability of tying with one or more opponents.
        /// </summary>
        public decimal TieProbability { get; init; }

        /// <summary>
        /// Probability of losing the hand.
        /// </summary>
        public decimal LoseProbability { get; init; }

        /// <summary>
        /// Expected share of the pot (1.0 = win full pot, 0.5 = split pot, etc.).
        /// </summary>
        public decimal ExpectedPotShare { get; init; }
    }

    /// <summary>
    /// Information about a player needed for odds calculation.
    /// </summary>
    public sealed record PlayerInfo
    {
        public required string Name { get; init; }
        public required IReadOnlyCollection<Card> KnownCards { get; init; }
        public int UnknownCardCount { get; init; }
    }

    /// <summary>
    /// Calculates odds for a Texas Hold'em hand.
    /// </summary>
    /// <param name="heroHoleCards">The hero's hole cards.</param>
    /// <param name="communityCards">Current community cards on the board.</param>
    /// <param name="opponentCount">Number of opponents still in the hand.</param>
    /// <param name="deadCards">Cards known to be out of play (folded hands, burned cards, etc.).</param>
    /// <param name="simulations">Number of Monte Carlo simulations to run.</param>
    public static OddsResult CalculateHoldemOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> communityCards,
        int opponentCount,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        
        var handTypeCounts = InitializeHandTypeCounts();
        int wins = 0, ties = 0, losses = 0;
        decimal totalPotShare = 0;

        var dealer = FrenchDeckDealer.WithFullDeck();

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            // Remove known cards from deck (deduplicate to handle overlapping collections)
            var knownCards = heroHoleCards.Concat(communityCards).Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            // Deal remaining community cards
            var remainingCommunityCount = 5 - communityCards.Count;
            var simulatedCommunity = communityCards.ToList();
            for (int j = 0; j < remainingCommunityCount; j++)
            {
                simulatedCommunity.Add(dealer.DealCard());
            }

            // Create hero's hand
            var heroHand = new HoldemHand(heroHoleCards, simulatedCommunity);
            handTypeCounts[heroHand.Type]++;

            // Deal and evaluate opponent hands
            var opponentHands = new List<HoldemHand>();
            for (int o = 0; o < opponentCount; o++)
            {
                var opponentHoleCards = dealer.DealCards(2);
                opponentHands.Add(new HoldemHand(opponentHoleCards.ToList(), simulatedCommunity));
            }

            // Compare hands and determine outcome
            var (outcome, potShare) = DetermineOutcome(heroHand, opponentHands);
            switch (outcome)
            {
                case Outcome.Win:
                    wins++;
                    break;
                case Outcome.Tie:
                    ties++;
                    break;
                case Outcome.Lose:
                    losses++;
                    break;
            }
            totalPotShare += potShare;
        }

        return CreateResult(handTypeCounts, wins, ties, losses, totalPotShare, simulations);
    }

    /// <summary>
    /// Calculates odds for a Seven Card Stud hand.
    /// </summary>
    /// <param name="heroHoleCards">The hero's hole cards (face down).</param>
    /// <param name="heroBoardCards">The hero's board cards (face up).</param>
    /// <param name="opponentBoardCards">List of opponent board cards that are visible.</param>
    /// <param name="totalCardsPerPlayer">Total number of cards each player will have at showdown (typically 7).</param>
    /// <param name="deadCards">Cards known to be out of play.</param>
    /// <param name="simulations">Number of Monte Carlo simulations to run.</param>
    public static OddsResult CalculateStudOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> heroBoardCards,
        IReadOnlyList<IReadOnlyCollection<Card>> opponentBoardCards,
        int totalCardsPerPlayer = 7,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        
        var handTypeCounts = InitializeHandTypeCounts();
        int wins = 0, ties = 0, losses = 0;
        decimal totalPotShare = 0;

        var dealer = FrenchDeckDealer.WithFullDeck();
        var heroTotalCards = heroHoleCards.Count + heroBoardCards.Count;
        var heroCardsNeeded = totalCardsPerPlayer - heroTotalCards;

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            // Remove known cards from deck (deduplicate to handle overlapping collections)
            var allKnownCards = heroHoleCards
                .Concat(heroBoardCards)
                .Concat(opponentBoardCards.SelectMany(b => b))
                .Concat(deadCards)
                .Distinct();
                
            foreach (var card in allKnownCards)
            {
                dealer.DealSpecific(card);
            }

            // Complete hero's hand
            var heroCards = heroHoleCards.Concat(heroBoardCards).ToList();
            for (int j = 0; j < heroCardsNeeded; j++)
            {
                heroCards.Add(dealer.DealCard());
            }

            // Create hero's stud hand (2 hole, 4 board, 1 final hole for 7-card stud)
            var heroHand = CreateStudHand(heroCards, totalCardsPerPlayer);
            handTypeCounts[heroHand.Type]++;

            // Deal and evaluate opponent hands
            var opponentHands = new List<HandBase>();
            foreach (var oppBoard in opponentBoardCards)
            {
                var oppCardsNeeded = totalCardsPerPlayer - oppBoard.Count;
                var oppCards = oppBoard.ToList();
                for (int j = 0; j < oppCardsNeeded; j++)
                {
                    oppCards.Add(dealer.DealCard());
                }
                opponentHands.Add(CreateStudHand(oppCards, totalCardsPerPlayer));
            }

            // Compare hands
            var (outcome, potShare) = DetermineOutcome(heroHand, opponentHands);
            switch (outcome)
            {
                case Outcome.Win:
                    wins++;
                    break;
                case Outcome.Tie:
                    ties++;
                    break;
                case Outcome.Lose:
                    losses++;
                    break;
            }
            totalPotShare += potShare;
        }

        return CreateResult(handTypeCounts, wins, ties, losses, totalPotShare, simulations);
    }

    /// <summary>
    /// Calculates odds for a Five Card Draw hand.
    /// </summary>
    /// <param name="heroCards">The hero's current hand.</param>
    /// <param name="opponentCount">Number of opponents still in the hand.</param>
    /// <param name="opponentDrawCounts">Optional: estimated number of cards each opponent will draw.</param>
    /// <param name="deadCards">Cards known to be out of play.</param>
    /// <param name="simulations">Number of Monte Carlo simulations to run.</param>
    public static OddsResult CalculateDrawOdds(
        IReadOnlyCollection<Card> heroCards,
        int opponentCount,
        IReadOnlyList<int> opponentDrawCounts = null,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        opponentDrawCounts ??= Enumerable.Repeat(0, opponentCount).ToList();
        
        var handTypeCounts = InitializeHandTypeCounts();
        int wins = 0, ties = 0, losses = 0;
        decimal totalPotShare = 0;

        var dealer = FrenchDeckDealer.WithFullDeck();

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            // Remove known cards from deck (deduplicate to handle overlapping collections)
            var knownCards = heroCards.Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            // Hero hand stays as is (no draw in this simulation - we're calculating current odds)
            var heroHand = new DrawHand(heroCards.ToList());
            handTypeCounts[heroHand.Type]++;

            // Deal opponent hands
            var opponentHands = new List<DrawHand>();
            for (int o = 0; o < opponentCount; o++)
            {
                var opponentCards = dealer.DealCards(5);
                opponentHands.Add(new DrawHand(opponentCards.ToList()));
            }

            // Compare hands
            var (outcome, potShare) = DetermineOutcome(heroHand, opponentHands.Cast<HandBase>().ToList());
            switch (outcome)
            {
                case Outcome.Win:
                    wins++;
                    break;
                case Outcome.Tie:
                    ties++;
                    break;
                case Outcome.Lose:
                    losses++;
                    break;
            }
            totalPotShare += potShare;
        }

        return CreateResult(handTypeCounts, wins, ties, losses, totalPotShare, simulations);
    }

    /// <summary>
    /// Calculates odds for an Omaha hand.
    /// </summary>
    /// <param name="heroHoleCards">The hero's 4 hole cards.</param>
    /// <param name="communityCards">Current community cards on the board.</param>
    /// <param name="opponentCount">Number of opponents still in the hand.</param>
    /// <param name="deadCards">Cards known to be out of play.</param>
    /// <param name="simulations">Number of Monte Carlo simulations to run.</param>
    public static OddsResult CalculateOmahaOdds(
        IReadOnlyCollection<Card> heroHoleCards,
        IReadOnlyCollection<Card> communityCards,
        int opponentCount,
        IReadOnlyCollection<Card> deadCards = null,
        int simulations = DefaultSimulations)
    {
        deadCards ??= Array.Empty<Card>();
        
        var handTypeCounts = InitializeHandTypeCounts();
        int wins = 0, ties = 0, losses = 0;
        decimal totalPotShare = 0;

        var dealer = FrenchDeckDealer.WithFullDeck();

        for (int i = 0; i < simulations; i++)
        {
            dealer.Shuffle();
            
            // Remove known cards from deck (deduplicate to handle overlapping collections)
            var knownCards = heroHoleCards.Concat(communityCards).Concat(deadCards).Distinct();
            foreach (var card in knownCards)
            {
                dealer.DealSpecific(card);
            }

            // Deal remaining community cards
            var remainingCommunityCount = 5 - communityCards.Count;
            var simulatedCommunity = communityCards.ToList();
            for (int j = 0; j < remainingCommunityCount; j++)
            {
                simulatedCommunity.Add(dealer.DealCard());
            }

            // Create hero's hand
            var heroHand = new OmahaHand(heroHoleCards, simulatedCommunity);
            handTypeCounts[heroHand.Type]++;

            // Deal and evaluate opponent hands
            var opponentHands = new List<OmahaHand>();
            for (int o = 0; o < opponentCount; o++)
            {
                var opponentHoleCards = dealer.DealCards(4);
                opponentHands.Add(new OmahaHand(opponentHoleCards.ToList(), simulatedCommunity));
            }

            // Compare hands
            var (outcome, potShare) = DetermineOutcome(heroHand, opponentHands.Cast<HandBase>().ToList());
            switch (outcome)
            {
                case Outcome.Win:
                    wins++;
                    break;
                case Outcome.Tie:
                    ties++;
                    break;
                case Outcome.Lose:
                    losses++;
                    break;
            }
            totalPotShare += potShare;
        }

        return CreateResult(handTypeCounts, wins, ties, losses, totalPotShare, simulations);
    }

    private enum Outcome { Win, Tie, Lose }

    private static Dictionary<HandType, int> InitializeHandTypeCounts()
    {
        return Enum.GetValues<HandType>()
            .Where(t => t != HandType.Incomplete)
            .ToDictionary(t => t, _ => 0);
    }

    private static (Outcome outcome, decimal potShare) DetermineOutcome(HandBase heroHand, IReadOnlyList<HandBase> opponentHands)
    {
        if (opponentHands.Count == 0)
        {
            return (Outcome.Win, 1.0m);
        }

        var maxOpponentStrength = opponentHands.Max(h => h.Strength);
        
        if (heroHand.Strength > maxOpponentStrength)
        {
            return (Outcome.Win, 1.0m);
        }
        
        if (heroHand.Strength < maxOpponentStrength)
        {
            return (Outcome.Lose, 0.0m);
        }
        
        // Hero ties with best opponent(s)
        var tiedCount = opponentHands.Count(h => h.Strength == heroHand.Strength) + 1; // +1 for hero
        return (Outcome.Tie, 1.0m / tiedCount);
    }

    private static HandBase CreateStudHand(IReadOnlyCollection<Card> cards, int totalCards)
    {
        // Ensure we have at least 7 cards for a valid stud hand
        if (cards.Count < 7)
        {
            throw new ArgumentException($"Expected at least 7 cards for stud hand, but got {cards.Count}");
        }
        
        var cardList = cards.ToList();
        
        // For 7-card stud: 2 hole cards, 4 board cards, 1 final hole card
        return new SevenCardStudHand(
            cardList.Take(2).ToList(),
            cardList.Skip(2).Take(4).ToList(),
            cardList.Last());
    }

    private static OddsResult CreateResult(
        Dictionary<HandType, int> handTypeCounts,
        int wins, int ties, int losses,
        decimal totalPotShare,
        int simulations)
    {
        var handTypeProbabilities = handTypeCounts
            .Where(kvp => kvp.Value > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (decimal)kvp.Value / simulations);

        return new OddsResult
        {
            HandTypeProbabilities = handTypeProbabilities,
            WinProbability = (decimal)wins / simulations,
            TieProbability = (decimal)ties / simulations,
            LoseProbability = (decimal)losses / simulations,
            ExpectedPotShare = totalPotShare / simulations
        };
    }
}
