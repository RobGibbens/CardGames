using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.KingsAndLows;

/// <summary>
/// Provides game rules metadata for Kings and Lows.
/// </summary>
public static class KingsAndLowsRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "KINGSANDLOWS",
            GameTypeName = "Kings and Lows",
            Description = "A five-card draw poker variant where kings and the lowest card are wild. Players ante, decide to drop or stay, draw cards, and losers match the pot.",
            MinPlayers = 2,
            MaxPlayers = 5,
            Phases = new List<GamePhaseDescriptor>
            {
                new() 
                { 
                    PhaseId = "WaitingToStart", 
                    Name = "Waiting to Start", 
                    Description = "Waiting for players to join and ready up",
                    Category = "Setup",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "CollectingAntes", 
                    Name = "Collecting Antes", 
                    Description = "Collecting ante bets from all players",
                    Category = "Setup",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "Dealing", 
                    Name = "Dealing", 
                    Description = "Dealing 5 cards to each player",
                    Category = "Dealing",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "DropOrStay", 
                    Name = "Drop or Stay", 
                    Description = "Players decide whether to drop out or stay in the hand",
                    Category = "Decision",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Drop", "Stay" }
                },
                new() 
                { 
                    PhaseId = "DrawPhase", 
                    Name = "Draw Phase", 
                    Description = "Players discard and draw replacement cards",
                    Category = "Drawing",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Draw" }
                },
                new() 
                { 
                    PhaseId = "PlayerVsDeck", 
                    Name = "Player vs Deck", 
                    Description = "Single remaining player competes against the deck",
                    Category = "Special",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "Showdown", 
                    Name = "Showdown", 
                    Description = "Players reveal hands and winner is determined",
                    Category = "Resolution",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "PotMatching", 
                    Name = "Pot Matching", 
                    Description = "Losers match the pot",
                    Category = "Resolution",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "Complete", 
                    Name = "Complete", 
                    Description = "Hand is complete",
                    Category = "Resolution",
                    RequiresPlayerAction = false,
                    IsTerminal = true
                }
            },
            CardDealing = new CardDealingConfig
            {
                InitialCards = 5,
                InitialVisibility = CardVisibility.FaceDown,
                HasCommunityCards = false
            },
            Betting = new BettingConfig
            {
                HasAntes = true,
                HasBlinds = false,
                BettingRounds = 0,
                Structure = "No Betting"
            },
            Drawing = new DrawingConfig
            {
                AllowsDrawing = true,
                MaxDiscards = 5,
                SpecialRules = "Players can discard all 5 cards",
                DrawingRounds = 1
            },
            Showdown = new ShowdownConfig
            {
                HandRanking = "Standard Poker (High) with Wild Cards",
                IsHighLow = false,
                HasSpecialSplitRules = true,
                SpecialSplitDescription = "Losers match the pot; pot carries over until someone wins it all"
            },
            SpecialRules = new Dictionary<string, object>
            {
                ["WildCards"] = "All Kings and each player's lowest card",
                ["DropOrStay"] = true,
                ["PotCarryOver"] = true,
                ["LosersMatchPot"] = true
            }
        };
    }
}
