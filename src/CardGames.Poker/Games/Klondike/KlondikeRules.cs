using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Klondike;

/// <summary>
/// Provides game rules metadata for Klondike Hold'em.
/// Standard Hold'em structure with an additional Klondike Card dealt face-down
/// immediately after hole cards, before the first betting round.
/// Acts as a wild card at showdown.
/// </summary>
public static class KlondikeRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "KLONDIKE",
            GameTypeName = "Klondike Hold'em",
            Description = "Texas Hold 'Em with a hidden wild card dealt after hole cards. At showdown, each player treats it as any rank and suit.",
            MinPlayers = 2,
            MaxPlayers = 10,
            Phases = new List<GamePhaseDescriptor>
            {
                new() { PhaseId = "WaitingToStart", Name = "Waiting to Start", Description = "Waiting for players", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "CollectingBlinds", Name = "Collecting Blinds", Description = "Collecting blinds", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "Dealing", Name = "Dealing", Description = "Dealing hole cards and Klondike Card", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "PreFlop", Name = "Pre-Flop", Description = "Initial betting round", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Flop", Name = "Flop", Description = "First 3 community cards", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Turn", Name = "Turn", Description = "4th community card", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "River", Name = "River", Description = "5th community card", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "KlondikeReveal", Name = "Klondike Reveal", Description = "Klondike wild card revealed — 20 second pause", Category = "Resolution", RequiresPlayerAction = false },
                new() { PhaseId = "Showdown", Name = "Showdown", Description = "Determine winner — Klondike Card revealed as wild", Category = "Resolution", RequiresPlayerAction = false },
                new() { PhaseId = "Complete", Name = "Complete", Description = "Hand complete", Category = "Resolution", RequiresPlayerAction = false, IsTerminal = true }
            },
            CardDealing = new CardDealingConfig
            {
                InitialCards = 2,
                InitialVisibility = CardVisibility.FaceDown,
                HasCommunityCards = true,
                DealingRounds = new List<DealingRound>
                {
                    new() { CardCount = 2, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Community }, // Klondike Card
                    new() { CardCount = 3, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Community },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Community },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Community }
                }
            },
            Betting = new BettingConfig
            {
                HasAntes = false,
                HasBlinds = true,
                BettingRounds = 4,
                Structure = "No Limit"
            },
            Showdown = new ShowdownConfig
            {
                HandRanking = "Standard",
                IsHighLow = false,
                HasSpecialSplitRules = false
            }
        };
    }
}
