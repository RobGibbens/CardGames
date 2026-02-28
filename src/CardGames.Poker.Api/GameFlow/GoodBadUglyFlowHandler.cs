using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.GoodBadUgly;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for "The Good, the Bad, and the Ugly" poker.
/// </summary>
/// <remarks>
/// <para>
/// A Seven Card Stud variant with three face-down table cards that are revealed
/// between streets with special effects:
/// </para>
/// <list type="number">
///   <item><description>Collect antes from all players</description></item>
///   <item><description>Third Street: Deal 2 down + 1 up card, bring-in betting</description></item>
///   <item><description>Fourth Street: Deal 1 up card, betting round (small bet)</description></item>
///   <item><description>Reveal "The Good": matching ranks become wild</description></item>
///   <item><description>Fifth Street: Deal 1 up card, betting round (big bet)</description></item>
///   <item><description>Reveal "The Bad": matching cards must be discarded</description></item>
///   <item><description>Sixth Street: Deal 1 up card, betting round (big bet)</description></item>
///   <item><description>Reveal "The Ugly": players with matching face-up cards are eliminated</description></item>
///   <item><description>Seventh Street: Deal 1 down card, final betting round (big bet)</description></item>
///   <item><description>Showdown (if multiple players remain)</description></item>
/// </list>
/// </remarks>
public sealed class GoodBadUglyFlowHandler : BaseGameFlowHandler
{
    /// <inheritdoc />
    public override string GameTypeCode => "GOODBADUGLY";

    /// <inheritdoc />
    public override GameRules GetGameRules() => GoodBadUglyRules.CreateGameRules();

    /// <inheritdoc />
    public override IReadOnlyList<string> SpecialPhases =>
    [
        nameof(Phases.RevealTheGood),
        nameof(Phases.RevealTheBad),
        nameof(Phases.RevealTheUgly)
    ];

    /// <inheritdoc />
    public override DealingConfiguration GetDealingConfiguration()
    {
        return new DealingConfiguration
        {
            PatternType = DealingPatternType.StreetBased,
            DealingRounds =
            [
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.ThirdStreet),
                    HoleCards = 2,
                    BoardCards = 1,
                    HasBettingAfter = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.FourthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.FifthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.SixthStreet),
                    HoleCards = 0,
                    BoardCards = 1,
                    HasBettingAfter = true
                },
                new DealingRoundConfig
                {
                    PhaseName = nameof(Phases.SeventhStreet),
                    HoleCards = 1,
                    BoardCards = 0,
                    HasBettingAfter = true
                }
            ]
        };
    }

    /// <inheritdoc />
    public override string? GetNextPhase(Game game, string currentPhase)
    {
        // Check if only one player remains - skip to showdown/complete
        if (IsSinglePlayerRemaining(game) && !IsResolutionPhase(currentPhase))
        {
            return nameof(Phases.Showdown);
        }

        return currentPhase switch
        {
            nameof(Phases.CollectingAntes) => nameof(Phases.ThirdStreet),
            nameof(Phases.ThirdStreet) => nameof(Phases.FourthStreet),
            nameof(Phases.FourthStreet) => nameof(Phases.RevealTheGood),
            nameof(Phases.RevealTheGood) => nameof(Phases.FifthStreet),
            nameof(Phases.FifthStreet) => nameof(Phases.RevealTheBad),
            nameof(Phases.RevealTheBad) => nameof(Phases.SixthStreet),
            nameof(Phases.SixthStreet) => nameof(Phases.RevealTheUgly),
            nameof(Phases.RevealTheUgly) => nameof(Phases.SeventhStreet),
            nameof(Phases.SeventhStreet) => nameof(Phases.Showdown),
            nameof(Phases.Showdown) => nameof(Phases.Complete),
            _ => base.GetNextPhase(game, currentPhase)
        };
    }

    /// <summary>
    /// Gets the street names for this game variant.
    /// </summary>
    public static IReadOnlyList<string> Streets =>
    [
        nameof(Phases.ThirdStreet),
        nameof(Phases.FourthStreet),
        nameof(Phases.FifthStreet),
        nameof(Phases.SixthStreet),
        nameof(Phases.SeventhStreet)
    ];

    /// <summary>
    /// Determines if the specified phase is a street (dealing + betting) phase.
    /// </summary>
    public static bool IsStreetPhase(string phase)
    {
        return Streets.Contains(phase);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Deals the initial Third Street cards plus 3 face-down table cards
    /// ("The Good", "The Bad", "The Ugly") from the deck.
    /// </remarks>
    public override async Task DealCardsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var deckCards = await context.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Deck)
            .OrderBy(gc => gc.DealOrder)
            .ToListAsync(cancellationToken);

        // Create and persist shuffled deck if none exists
        if (deckCards.Count == 0)
        {
            var deck = CreateShuffledDeck();
            var generatedDeckCards = new List<GameCard>();
            var deckOrder = 0;

            foreach (var (suit, symbol) in deck)
            {
                var gameCard = new GameCard
                {
                    GameId = game.Id,
                    GamePlayerId = null,
                    HandNumber = game.CurrentHandNumber,
                    Suit = suit,
                    Symbol = symbol,
                    DealOrder = deckOrder++,
                    Location = CardLocation.Deck,
                    IsVisible = false,
                    IsDiscarded = false,
                    DealtAt = now
                };
                generatedDeckCards.Add(gameCard);
                context.GameCards.Add(gameCard);
            }

            deckCards = generatedDeckCards;
        }

        var deckIndex = 0;

        // Deal 3 table cards face-down (The Good, The Bad, The Ugly)
        var tableCardNames = new[] { "TheGood", "TheBad", "TheUgly" };
        for (var t = 0; t < 3; t++)
        {
            if (deckIndex >= deckCards.Count) break;

            var tableCard = deckCards[deckIndex++];
            tableCard.GamePlayerId = null;
            tableCard.Location = CardLocation.Community;
            tableCard.DealOrder = t;
            tableCard.IsVisible = false; // face-down until revealed
            tableCard.DealtAtPhase = tableCardNames[t];
            tableCard.DealtAt = now;
        }

        // Sort players starting from left of dealer
        var dealerPosition = game.DealerPosition;
        var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
        var totalSeats = maxSeatPosition + 1;

        var playersInDealOrder = eligiblePlayers
            .OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
            .ToList();

        var playerUpCards = new List<(GamePlayer Player, GameCard UpCard)>();

        // Deal Third Street: 2 hole cards + 1 board card per player
        var dealOrder = 10; // Start after table cards
        foreach (var player in playersInDealOrder)
        {
            for (var i = 0; i < 2; i++)
            {
                if (deckIndex >= deckCards.Count) break;

                var card = deckCards[deckIndex++];
                card.GamePlayerId = player.Id;
                card.Location = CardLocation.Hole;
                card.DealOrder = dealOrder++;
                card.IsVisible = false;
                card.DealtAtPhase = nameof(Phases.ThirdStreet);
                card.DealtAt = now;
            }

            if (deckIndex >= deckCards.Count) break;

            var boardCard = deckCards[deckIndex++];
            boardCard.GamePlayerId = player.Id;
            boardCard.Location = CardLocation.Board;
            boardCard.DealOrder = dealOrder++;
            boardCard.IsVisible = true;
            boardCard.DealtAtPhase = nameof(Phases.ThirdStreet);
            boardCard.DealtAt = now;

            playerUpCards.Add((player, boardCard));
        }

        // Reset CurrentBet for all players
        foreach (var player in game.GamePlayers)
        {
            player.CurrentBet = 0;
        }

        // Determine bring-in player (lowest up card)
        var bringInPlayer = FindBringInPlayer(playerUpCards);
        var bringInSeatPosition = bringInPlayer?.SeatPosition ??
            playersInDealOrder.FirstOrDefault()?.SeatPosition ?? 0;

        // Post bring-in bet if configured
        var bringIn = game.BringIn ?? 0;
        var currentBet = 0;
        if (bringIn > 0 && bringInPlayer is not null)
        {
            var actualBringIn = Math.Min(bringIn, bringInPlayer.ChipStack);
            bringInPlayer.CurrentBet = actualBringIn;
            bringInPlayer.ChipStack -= actualBringIn;
            bringInPlayer.TotalContributedThisHand += actualBringIn;
            currentBet = actualBringIn;

            var pot = await context.Pots
                .FirstOrDefaultAsync(p => p.GameId == game.Id &&
                                          p.HandNumber == game.CurrentHandNumber &&
                                          p.PotType == PotType.Main,
                                 cancellationToken);
            if (pot is not null)
            {
                pot.Amount += actualBringIn;
            }
        }

        // Create betting round for Third Street
        var minBet = game.SmallBet ?? game.MinBet ?? 0;
        var bettingRound = new Data.Entities.BettingRound
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            RoundNumber = 1,
            Street = nameof(Phases.ThirdStreet),
            CurrentBet = currentBet,
            MinBet = minBet,
            RaiseCount = 0,
            MaxRaises = 0,
            LastRaiseAmount = 0,
            PlayersInHand = eligiblePlayers.Count,
            PlayersActed = 0,
            CurrentActorIndex = bringInSeatPosition,
            LastAggressorIndex = -1,
            IsComplete = false,
            StartedAt = now
        };

        context.Set<Data.Entities.BettingRound>().Add(bettingRound);

        // Update game state
        game.CurrentPhase = nameof(Phases.ThirdStreet);
        game.CurrentPlayerIndex = bringInSeatPosition;
        game.BringInPlayerIndex = bringInSeatPosition;
        game.UpdatedAt = now;

        await context.SaveChangesAsync(cancellationToken);
    }

    #region Bring-In Helpers

    private static GamePlayer? FindBringInPlayer(List<(GamePlayer Player, GameCard UpCard)> playerUpCards)
    {
        if (playerUpCards.Count == 0)
        {
            return null;
        }

        GamePlayer? lowestPlayer = null;
        GameCard? lowestCard = null;

        foreach (var (player, upCard) in playerUpCards)
        {
            if (lowestCard is null || CompareCardsForBringIn(upCard, lowestCard) < 0)
            {
                lowestCard = upCard;
                lowestPlayer = player;
            }
        }

        return lowestPlayer;
    }

    private static int CompareCardsForBringIn(GameCard a, GameCard b)
    {
        var aValue = GetCardValue(a.Symbol);
        var bValue = GetCardValue(b.Symbol);

        if (aValue != bValue)
        {
            return aValue.CompareTo(bValue);
        }

        return GetSuitRank(a.Suit).CompareTo(GetSuitRank(b.Suit));
    }

    private static int GetCardValue(CardSymbol symbol) => symbol switch
    {
        CardSymbol.Deuce => 2,
        CardSymbol.Three => 3,
        CardSymbol.Four => 4,
        CardSymbol.Five => 5,
        CardSymbol.Six => 6,
        CardSymbol.Seven => 7,
        CardSymbol.Eight => 8,
        CardSymbol.Nine => 9,
        CardSymbol.Ten => 10,
        CardSymbol.Jack => 11,
        CardSymbol.Queen => 12,
        CardSymbol.King => 13,
        CardSymbol.Ace => 14,
        _ => 0
    };

    private static int GetSuitRank(CardSuit suit) => suit switch
    {
        CardSuit.Clubs => 0,
        CardSuit.Diamonds => 1,
        CardSuit.Hearts => 2,
        CardSuit.Spades => 3,
        _ => 0
    };

    #endregion
}
