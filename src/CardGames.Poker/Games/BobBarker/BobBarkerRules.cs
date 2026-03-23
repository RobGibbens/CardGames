using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.BobBarker;

/// <summary>
/// Provides game rules metadata for Bob Barker.
/// </summary>
public static class BobBarkerRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "BOBBARKER",
            GameTypeName = "Bob Barker",
            Description = "Deal five hole cards, select one showcase card against a hidden dealer card, then continue as Omaha with the remaining four hole cards.",
            MinPlayers = 2,
            MaxPlayers = 10,
            Phases = new List<GamePhaseDescriptor>
            {
                new() { PhaseId = "WaitingToStart", Name = "Waiting to Start", Description = "Waiting for players", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "CollectingBlinds", Name = "Collecting Blinds", Description = "Collecting blinds", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "Dealing", Name = "Dealing", Description = "Dealing five hole cards and the hidden dealer card", Category = "Setup", RequiresPlayerAction = false },
                new() { PhaseId = "DrawPhase", Name = "Choose Showcase", Description = "Each player selects one showcase card before betting begins", Category = "Drawing", RequiresPlayerAction = true, AvailableActions = ["ChooseShowcase"] },
                new() { PhaseId = "PreFlop", Name = "Pre-Flop", Description = "Initial betting round with four active hole cards", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Flop", Name = "Flop", Description = "First 3 community cards", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Turn", Name = "Turn", Description = "4th community card", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "River", Name = "River", Description = "5th community card", Category = "Betting", RequiresPlayerAction = true },
                new() { PhaseId = "Showdown", Name = "Showdown", Description = "Award half the pot to the Omaha winner and half to the best showcase card", Category = "Resolution", RequiresPlayerAction = false },
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
                    new() { CardCount = 1, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Community },
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
            Drawing = new DrawingConfig
            {
                AllowsDrawing = true,
                MaxDiscards = 1,
                SpecialRules = "Select exactly 1 showcase card before pre-flop betting; the remaining 4 cards play as Omaha"
            },
            Showdown = new ShowdownConfig
            {
                HandRanking = "BobBarker",
                IsHighLow = false,
                HasSpecialSplitRules = true
            },
            SpecialRules = new Dictionary<string, object>
            {
                ["BobBarker"] = "Half the pot goes to the best Omaha hand. Half goes to the showcase card closest to the hidden dealer card without going over.",
                ["ShowcaseSelection"] = "Each player must choose exactly one showcase card before pre-flop betting.",
                ["DealerCardReveal"] = "The dealer card and showcase cards remain hidden until showdown."
            }
        };
    }
}