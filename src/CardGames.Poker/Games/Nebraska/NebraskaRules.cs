using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Nebraska;

/// <summary>
/// Provides game rules metadata for Nebraska.
/// </summary>
public static class NebraskaRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "NEBRASKA",
            GameTypeName = "Nebraska",
            Description = "A community card poker game similar to Omaha, but players receive five hole cards and must use exactly three hole cards with exactly two community cards.",
            MinPlayers = 2,
            MaxPlayers = 10,
            Phases = new List<GamePhaseDescriptor>
            {
                new() { PhaseId = "WaitingToStart", Name = "Waiting to Start", Description = "Waiting for players", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "CollectingBlinds", Name = "Collecting Blinds", Description = "Collecting blinds", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "Dealing", Name = "Dealing", Description = "Dealing hole cards", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "PreFlop", Name = "Pre-Flop", Description = "Initial betting round", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Flop", Name = "Flop", Description = "First 3 community cards", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Turn", Name = "Turn", Description = "4th community card", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "River", Name = "River", Description = "5th community card", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Showdown", Name = "Showdown", Description = "Determine winner", Category = "Resolution", RequiresPlayerAction = false },
                new() { PhaseId = "Complete", Name = "Complete", Description = "Hand complete", Category = "Resolution", RequiresPlayerAction = false, IsTerminal = true }
            },
            CardDealing = new CardDealingConfig
            {
                InitialCards = 5,
                InitialVisibility = CardVisibility.FaceDown,
                HasCommunityCards = true,
                DealingRounds = new List<DealingRound>
                {
                    new() { CardCount = 5, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
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
                Structure = "Pot Limit"
            },
            Showdown = new ShowdownConfig
            {
                HandRanking = "Nebraska",
                IsHighLow = false,
                HasSpecialSplitRules = false
            }
        };
    }
}
