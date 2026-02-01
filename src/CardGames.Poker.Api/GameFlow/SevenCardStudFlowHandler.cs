using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.SevenCardStud;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Seven Card Stud poker.
/// </summary>
/// <remarks>
/// <para>
/// Seven Card Stud is a classic stud poker variant with street-based dealing:
/// </para>
/// <list type="number">
///   <item><description>Collect antes from all players</description></item>
///   <item><description>Third Street: Deal 2 down cards and 1 up card, bring-in betting</description></item>
///   <item><description>Fourth Street: Deal 1 up card, betting round (small bet)</description></item>
///   <item><description>Fifth Street: Deal 1 up card, betting round (big bet)</description></item>
///   <item><description>Sixth Street: Deal 1 up card, betting round (big bet)</description></item>
///   <item><description>Seventh Street: Deal 1 down card, final betting round (big bet)</description></item>
///   <item><description>Showdown (if multiple players remain)</description></item>
/// </list>
/// <para>
/// Each player receives 7 cards total: 3 face-down (2 initial + river) and 4 face-up.
/// Best 5-card hand wins.
/// </para>
/// </remarks>
public sealed class SevenCardStudFlowHandler : BaseGameFlowHandler
{
    /// <inheritdoc />
    public override string GameTypeCode => "SEVENCARDSTUD";

    /// <inheritdoc />
    public override GameRules GetGameRules() => SevenCardStudRules.CreateGameRules();

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

        // Seven Card Stud has explicit street-based phase transitions
        return currentPhase switch
        {
            nameof(Phases.CollectingAntes) => nameof(Phases.ThirdStreet),
            nameof(Phases.ThirdStreet) => nameof(Phases.FourthStreet),
            nameof(Phases.FourthStreet) => nameof(Phases.FifthStreet),
            nameof(Phases.FifthStreet) => nameof(Phases.SixthStreet),
            nameof(Phases.SixthStreet) => nameof(Phases.SeventhStreet),
            nameof(Phases.SeventhStreet) => nameof(Phases.Showdown),
            nameof(Phases.Showdown) => nameof(Phases.Complete),
            _ => base.GetNextPhase(game, currentPhase)
        };
    }

    /// <summary>
    /// Gets the street names for Seven Card Stud.
    /// </summary>
    /// <remarks>
    /// Streets are the dealing/betting rounds in stud games:
    /// Third Street through Seventh Street correspond to the 3rd through 7th cards dealt.
    /// </remarks>
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
    /// <param name="phase">The phase to check.</param>
    /// <returns>True if the phase is a street; otherwise, false.</returns>
    public static bool IsStreetPhase(string phase)
    {
        return Streets.Contains(phase);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Deals the initial Third Street cards for Seven Card Stud:
    /// 2 hole cards (face down) + 1 board card (face up) for each player.
    /// Also sets up the bring-in betting round.
    /// </remarks>
    public override async Task DealCardsAsync(
        CardsDbContext context,
        Game game,
        List<GamePlayer> eligiblePlayers,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var deck = CreateShuffledDeck();
        var deckCards = new List<GameCard>();
        var deckOrder = 0;

        // Persist entire shuffled deck
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
            deckCards.Add(gameCard);
            context.GameCards.Add(gameCard);
        }

        var deckIndex = 0;

        // Sort players starting from left of dealer
        var dealerPosition = game.DealerPosition;
        var maxSeatPosition = game.GamePlayers.Max(gp => gp.SeatPosition);
        var totalSeats = maxSeatPosition + 1;

        var playersInDealOrder = eligiblePlayers
            .OrderBy(p => (p.SeatPosition - dealerPosition - 1 + totalSeats) % totalSeats)
            .ToList();

        // Track up cards for bring-in determination
        var playerUpCards = new List<(GamePlayer Player, GameCard UpCard)>();

        // Deal Third Street: 2 hole cards + 1 board card per player
        var dealOrder = 1;
        foreach (var player in playersInDealOrder)
        {
            // 2 hole cards (face down)
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

            // 1 board card (face up)
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

            // Add to pot
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

    /// <summary>
    /// Finds the player with the lowest up card for bring-in determination.
    /// Lower card value loses. For ties, use suit order: clubs (lowest) &lt; diamonds &lt; hearts &lt; spades (highest).
    /// </summary>
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

    /// <summary>
    /// Compares two cards for bring-in determination.
    /// Lower value is "worse" (brings in). For ties, lower suit brings in.
    /// Suit order: Clubs (0) &lt; Diamonds (1) &lt; Hearts (2) &lt; Spades (3)
    /// </summary>
    private static int CompareCardsForBringIn(GameCard a, GameCard b)
    {
        var aValue = GetCardValue(a.Symbol);
        var bValue = GetCardValue(b.Symbol);

        if (aValue != bValue)
        {
            return aValue.CompareTo(bValue);
        }

        // Suit order for ties
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
}
