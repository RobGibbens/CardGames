using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.FollowTheQueen;
using CardGames.Poker.Games.GameFlow;

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
        // Deal Third Street: 2 down, 1 up
        var deck = CreateShuffledDeck();
        var deckCards = new List<GameCard>();
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

        // Deal 2 face down, 1 face up
        
        // 1st Hole Card
        foreach (var player in playersInDealOrder)
        {
             var card = deckCards[deckIndex++];
             card.GamePlayerId = player.Id;
             card.Location = CardLocation.Hand;
             card.DealOrder = 1;
             card.IsVisible = false;
        }

        // 2nd Hole Card
        foreach (var player in playersInDealOrder)
        {
             var card = deckCards[deckIndex++];
             card.GamePlayerId = player.Id;
             card.Location = CardLocation.Hand;
             card.DealOrder = 2;
             card.IsVisible = false;
        }

        // 3rd Card (Door card / Up card)
        foreach (var player in playersInDealOrder)
        {
             var card = deckCards[deckIndex++];
             card.GamePlayerId = player.Id;
             card.Location = CardLocation.Hand;
             card.DealOrder = 3;
             card.IsVisible = true;
        }
        
        // Reset bets
        foreach (var player in game.GamePlayers)
        {
            player.CurrentBet = 0;
        }

        // Set next phase
        game.CurrentPhase = nameof(Phases.ThirdStreet);
        
        // Set Bring-In logic?
        // SevenCardStud sets Bring-In player index.
        // Usually the lowest or highest up card.
        // BaseGameFlowHandler doesn't know this logic.
        // But SevenCardStudFlowHandler doesn't seem to implement it in DealCardsAsync either.
        // It relies on API Command handlers to process "Join" "Start", or maybe just leaves CurrentPlayerIndex as -1 
        // and expecting client to prompt?
        
        // Wait, ContinuousPlayBackgroundService sets CurrentPlayerIndex = -1.
        // Someone must set the current player.
        // Usually DetermineFirstPlayer logic.
        
        // For now, I'll stick to dealing cards.
    }
}
