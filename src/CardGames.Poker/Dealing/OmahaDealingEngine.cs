using System;
using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Decks;
using CardGames.Core.Dealer;

namespace CardGames.Poker.Dealing;

/// <summary>
/// Dealing engine for Omaha.
/// Handles dealing 4 hole cards per player and 5 community cards (3 flop, 1 turn, 1 river).
/// Burns one card before each community card round.
/// </summary>
public class OmahaDealingEngine : IDealingEngine
{
    private Dealer<Card> _dealer;
    private SeededRandomNumberGenerator _rng;
    private IReadOnlyList<string> _playerNames;
    private int _dealerPosition;
    private int _dealSequence;

    /// <inheritdoc/>
    public string VariantId => "omaha";

    /// <inheritdoc/>
    public int Seed => _rng?.Seed ?? 0;

    /// <inheritdoc/>
    public int HoleCardsPerPlayer => 4;

    /// <inheritdoc/>
    public bool UsesCommunityCards => true;

    /// <inheritdoc/>
    public bool UsesBurnCards => true;

    /// <inheritdoc/>
    public int CardsRemaining => _deck?.NumberOfCardsLeft() ?? 0;

    private FullFrenchDeck _deck;

    /// <inheritdoc/>
    public void Initialize(IReadOnlyList<string> playerNames, int dealerPosition, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(playerNames);

        if (playerNames.Count < 2)
        {
            throw new ArgumentException("Omaha requires at least 2 players", nameof(playerNames));
        }

        _playerNames = playerNames;
        _dealerPosition = dealerPosition;
        _dealSequence = 0;

        if (seed.HasValue)
        {
            _rng = new SeededRandomNumberGenerator(seed.Value);
        }
        else
        {
            var (rng, _) = SeededRandomNumberGenerator.CreateWithRandomSeed();
            _rng = rng;
        }

        _deck = new FullFrenchDeck();
        _dealer = new Dealer<Card>(_deck, _rng);
    }

    /// <inheritdoc/>
    public void Shuffle()
    {
        _dealer.Shuffle();
        _dealSequence = 0;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DealResult> DealHoleCards()
    {
        var results = new List<DealResult>();
        var playerCount = _playerNames.Count;

        // Deal 4 cards to each player, starting left of dealer
        // Cards are dealt one at a time, rotating around the table
        for (int cardRound = 0; cardRound < HoleCardsPerPlayer; cardRound++)
        {
            for (int i = 0; i < playerCount; i++)
            {
                // Start with small blind position (left of dealer)
                var playerIndex = (_dealerPosition + 1 + i) % playerCount;
                var playerName = _playerNames[playerIndex];
                var card = _dealer.DealCard();

                results.Add(new DealResult(
                    card,
                    playerName,
                    DealCardType.HoleCard,
                    ++_dealSequence));
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DealResult> DealCommunityCards(string streetName)
    {
        var results = new List<DealResult>();

        // Burn one card
        var burnCard = _dealer.DealCard();
        results.Add(new DealResult(
            burnCard,
            "Burn",
            DealCardType.BurnCard,
            ++_dealSequence));

        // Determine number of cards to deal based on street
        var cardCount = streetName.ToUpperInvariant() switch
        {
            "FLOP" => 3,
            "TURN" => 1,
            "RIVER" => 1,
            _ => throw new ArgumentException($"Unknown street: {streetName}", nameof(streetName))
        };

        // Deal community cards
        for (int i = 0; i < cardCount; i++)
        {
            var card = _dealer.DealCard();
            results.Add(new DealResult(
                card,
                "Community",
                DealCardType.CommunityCard,
                ++_dealSequence));
        }

        return results;
    }

    /// <inheritdoc/>
    public DealResult DealToPlayer(string playerName, bool faceUp)
    {
        var card = _dealer.DealCard();
        return new DealResult(
            card,
            playerName,
            faceUp ? DealCardType.FaceUpCard : DealCardType.HoleCard,
            ++_dealSequence);
    }
}
