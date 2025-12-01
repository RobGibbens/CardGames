using System;
using System.Collections.Generic;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Decks;
using CardGames.Core.Dealer;

namespace CardGames.Poker.Dealing;

/// <summary>
/// Dealing engine for Seven Card Stud.
/// Handles dealing cards in the proper order and visibility for stud games.
/// Third street: 2 hole cards + 1 face-up card
/// Fourth-Sixth street: 1 face-up card each
/// Seventh street: 1 hole card (face down)
/// </summary>
public class SevenCardStudDealingEngine : IDealingEngine
{
    private Dealer<Card> _dealer;
    private SeededRandomNumberGenerator _rng;
    private IReadOnlyList<string> _playerNames;
    private int _dealerPosition;
    private int _dealSequence;
    private FullFrenchDeck _deck;
    private int _currentStreet;

    /// <inheritdoc/>
    public string VariantId => "seven-card-stud";

    /// <inheritdoc/>
    public int Seed => _rng?.Seed ?? 0;

    /// <inheritdoc/>
    public int HoleCardsPerPlayer => 3; // 2 initial + 1 at seventh street

    /// <inheritdoc/>
    public bool UsesCommunityCards => false;

    /// <inheritdoc/>
    public bool UsesBurnCards => false;

    /// <inheritdoc/>
    public int CardsRemaining => _deck?.NumberOfCardsLeft() ?? 0;

    /// <summary>
    /// Gets the current street number (3-7).
    /// </summary>
    public int CurrentStreet => _currentStreet;

    /// <inheritdoc/>
    public void Initialize(IReadOnlyList<string> playerNames, int dealerPosition, int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(playerNames);

        if (playerNames.Count < 2)
        {
            throw new ArgumentException("Seven Card Stud requires at least 2 players", nameof(playerNames));
        }

        if (playerNames.Count > 8)
        {
            throw new ArgumentException("Seven Card Stud supports at most 8 players", nameof(playerNames));
        }

        _playerNames = playerNames;
        _dealerPosition = dealerPosition;
        _dealSequence = 0;
        _currentStreet = 3;

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
        _currentStreet = 3;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DealResult> DealHoleCards()
    {
        if (_currentStreet != 3)
        {
            throw new InvalidOperationException("Hole cards can only be dealt on third street");
        }

        var results = new List<DealResult>();
        var playerCount = _playerNames.Count;

        // Third street: deal 2 hole cards + 1 face-up card to each player
        // First, deal 2 hole cards (face down) to each player
        for (int cardRound = 0; cardRound < 2; cardRound++)
        {
            for (int i = 0; i < playerCount; i++)
            {
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

        // Then deal 1 face-up card (door card) to each player
        for (int i = 0; i < playerCount; i++)
        {
            var playerIndex = (_dealerPosition + 1 + i) % playerCount;
            var playerName = _playerNames[playerIndex];
            var card = _dealer.DealCard();

            results.Add(new DealResult(
                card,
                playerName,
                DealCardType.FaceUpCard,
                ++_dealSequence));
        }

        return results;
    }

    /// <inheritdoc/>
    public IReadOnlyList<DealResult> DealCommunityCards(string streetName)
    {
        // Seven Card Stud doesn't use community cards
        throw new InvalidOperationException("Seven Card Stud does not use community cards. Use DealStreet instead.");
    }

    /// <summary>
    /// Deals cards for a specific street in stud.
    /// </summary>
    /// <param name="streetNumber">The street number (4-7).</param>
    /// <param name="activePlayers">The players still in the hand.</param>
    /// <returns>The sequence of deal operations.</returns>
    public IReadOnlyList<DealResult> DealStreet(int streetNumber, IReadOnlyList<string> activePlayers)
    {
        if (streetNumber < 4 || streetNumber > 7)
        {
            throw new ArgumentException("Street number must be between 4 and 7", nameof(streetNumber));
        }

        if (streetNumber != _currentStreet + 1)
        {
            throw new InvalidOperationException($"Expected to deal street {_currentStreet + 1}, but asked to deal street {streetNumber}");
        }

        var results = new List<DealResult>();
        var isFaceDown = streetNumber == 7; // Seventh street is dealt face down

        foreach (var playerName in activePlayers)
        {
            var card = _dealer.DealCard();
            var cardType = isFaceDown ? DealCardType.HoleCard : DealCardType.FaceUpCard;

            results.Add(new DealResult(
                card,
                playerName,
                cardType,
                ++_dealSequence));
        }

        _currentStreet = streetNumber;
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

    /// <summary>
    /// Gets the street name for display.
    /// </summary>
    /// <param name="streetNumber">The street number (3-7).</param>
    /// <returns>The display name of the street.</returns>
    public static string GetStreetName(int streetNumber)
    {
        return streetNumber switch
        {
            3 => "Third Street",
            4 => "Fourth Street",
            5 => "Fifth Street",
            6 => "Sixth Street",
            7 => "Seventh Street",
            _ => $"Street {streetNumber}"
        };
    }
}
