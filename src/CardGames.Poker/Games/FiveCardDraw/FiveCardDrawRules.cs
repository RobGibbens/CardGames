using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.FiveCardDraw;

/// <summary>
/// Provides game rules metadata for Five Card Draw.
/// </summary>
public static class FiveCardDrawRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "FIVECARDDRAW",
            GameTypeName = "Five Card Draw",
            Description = "A classic poker variant where players are dealt five cards, have a draw phase to exchange cards, and engage in two betting rounds before showdown.",
            MinPlayers = 2,
            MaxPlayers = 6,
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
                    PhaseId = "FirstBettingRound", 
                    Name = "First Betting Round", 
                    Description = "Players bet, raise, call, or fold",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
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
                    PhaseId = "DrawComplete", 
                    Name = "Draw Complete", 
                    Description = "All players have completed their draws",
                    Category = "Drawing",
                    RequiresPlayerAction = false
                },
                new() 
                { 
                    PhaseId = "SecondBettingRound", 
                    Name = "Second Betting Round", 
                    Description = "Final betting round before showdown",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
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
                BettingRounds = 2,
                Structure = "Fixed Limit"
            },
            Drawing = new DrawingConfig
            {
                AllowsDrawing = true,
                MaxDiscards = 3,
                SpecialRules = "Players can discard up to 4 cards if they have an Ace",
                DrawingRounds = 1
            },
            Showdown = new ShowdownConfig
            {
                HandRanking = "Standard Poker (High)",
                IsHighLow = false,
                HasSpecialSplitRules = false
            }
        };
    }
}
