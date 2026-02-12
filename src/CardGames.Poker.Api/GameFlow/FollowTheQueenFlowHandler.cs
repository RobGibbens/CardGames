using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FollowTheQueen;
using CardGames.Poker.Games.GameFlow;
using Microsoft.EntityFrameworkCore;

namespace CardGames.Poker.Api.GameFlow;

/// <summary>
/// Game flow handler for Follow the Queen poker.
/// </summary>
public sealed class FollowTheQueenFlowHandler : BaseGameFlowHandler
{
    public override string GameTypeCode => "FOLLOWTHEQUEEN";

    public override GameRules GetGameRules() => FollowTheQueenRules.CreateGameRules();

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

    public override string? GetNextPhase(Game game, string currentPhase)
    {
        // Check if only one player remains - skip to showdown/complete
        // We use string constant since IsSinglePlayerRemaining might check specific phase names
        // But logic is cleaner here.
        
        // Note: Base class has logic for next phase finding via Rules.
        // But SevenCardStud overrides it to be explicit about Streets.
        // We can do same.
        
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
        
        // Reset current bets for all players
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

        // Set current phase to ThirdStreet (betting round)
        game.CurrentPhase = nameof(Phases.ThirdStreet);
        game.CurrentPlayerIndex = bringInSeatPosition;

        // Create the initial betting round for Third Street
        var bettingRound = new CardGames.Poker.Api.Data.Entities.BettingRound
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            RoundNumber = 1,
            Street = nameof(Phases.ThirdStreet),
            CurrentBet = currentBet,
            MinBet = game.SmallBet ?? 0,
            MaxRaises = 4, // Fixed limit typically caps raises
            LastRaiseAmount = 0,
            PlayersInHand = eligiblePlayers.Count,
            PlayersActed = 0,
            CurrentActorIndex = bringInSeatPosition,
            IsComplete = false,
            StartedAt = now
        };

        if (currentBet > 0 && bringInPlayer is not null)
        {
            bettingRound.LastAggressorIndex = bringInPlayer.SeatPosition;
        }

        context.Set<CardGames.Poker.Api.Data.Entities.BettingRound>().Add(bettingRound);
    }

    /// <summary>
    /// Finds the player with the lowest up card for bring-in.
    /// </summary>
    private static GamePlayer? FindBringInPlayer(List<(GamePlayer Player, GameCard UpCard)> playerUpCards)
    {
        if (playerUpCards.Count == 0) return null;

        // Find lowest card by rank (Ace is low for bring-in)
        // Then by suit if tied (Clubs < Diamonds < Hearts < Spades)
        var lowestCard = playerUpCards
            .OrderBy(p => GetRankValue(p.UpCard.Symbol))
            .ThenBy(p => p.UpCard.Suit)
            .First();

        return lowestCard.Player;
    }

    /// <summary>
    /// Gets the rank value for bring-in determination (Ace = 1, King = 13).
    /// </summary>
    private static int GetRankValue(CardSymbol symbol) => symbol switch
    {
        CardSymbol.Ace => 1,
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
        _ => 0
    };
}
