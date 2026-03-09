using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.PhilsMom;

/// <summary>
/// Provides game rules metadata for Phil's Mom.
/// </summary>
public static class PhilsMomRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "PHILSMOM",
            GameTypeName = "Phil's Mom",
            Description = "Deal 4 hole cards, discard 1 before the flop and 1 after the flop, then play Hold 'Em with community cards.",
            MinPlayers = 2,
            MaxPlayers = 10,
            Phases = new List<GamePhaseDescriptor>
            {
                new() { PhaseId = "WaitingToStart", Name = "Waiting to Start", Description = "Waiting for players", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "CollectingBlinds", Name = "Collecting Blinds", Description = "Collecting blinds", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "Dealing", Name = "Dealing", Description = "Dealing hole cards", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "PreFlop", Name = "Pre-Flop", Description = "Initial betting round", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "DrawPhase", Name = "Discard", Description = "Discard 1 card", Category = "Drawing", RequiresPlayerAction = true },
                new() { PhaseId = "Flop", Name = "Flop", Description = "First 3 community cards", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Turn", Name = "Turn", Description = "4th community card", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "River", Name = "River", Description = "5th community card", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Showdown", Name = "Showdown", Description = "Determine winner", Category = "Resolution", RequiresPlayerAction = false },
                new() { PhaseId = "Complete", Name = "Complete", Description = "Hand complete", Category = "Resolution", RequiresPlayerAction = false, IsTerminal = true }
            },
            CardDealing = new CardDealingConfig
            {
                InitialCards = 4,
                InitialVisibility = CardVisibility.FaceDown,
                HasCommunityCards = true,
                DealingRounds = new List<DealingRound>
                {
                    new() { CardCount = 4, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
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
            Drawing = new DrawingConfig
            {
                AllowsDrawing = true,
                MaxDiscards = 1,
                SpecialRules = "Discard exactly 1 card before flop and exactly 1 card after flop"
            },
            Showdown = new ShowdownConfig
            {
                HandRanking = "HoldEm",
                IsHighLow = false,
                HasSpecialSplitRules = false
            }
        };
    }
}
