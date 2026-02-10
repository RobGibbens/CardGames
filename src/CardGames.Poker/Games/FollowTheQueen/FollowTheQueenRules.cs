using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.FollowTheQueen;

/// <summary>
/// Provides game rules metadata for Follow the Queen.
/// </summary>
public static class FollowTheQueenRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "FOLLOWTHEQUEEN",
            GameTypeName = "Follow the Queen",
            Description = "A seven card stud poker variant where Queens are wild, and the card following the last face-up Queen also becomes wild.",
            MinPlayers = 2,
            MaxPlayers = 7,
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
                    PhaseId = "ThirdStreet", 
                    Name = "Third Street", 
                    Description = "2 down cards and 1 up card dealt, bring-in betting round", 
                    Category = "Betting", 
                    RequiresPlayerAction = true, 
                    AvailableActions = new[] { "Call", "Raise", "Fold", "AllIn" } 
                },
                new() 
                { 
                    PhaseId = "FourthStreet", 
                    Name = "Fourth Street", 
                    Description = "1 up card dealt", 
                    Category = "Betting", 
                    RequiresPlayerAction = true, 
                    AvailableActions = new[] { "Check", "Bet", "Fold", "AllIn" } 
                },
                new() 
                { 
                    PhaseId = "FifthStreet", 
                    Name = "Fifth Street", 
                    Description = "1 up card dealt", 
                    Category = "Betting", 
                    RequiresPlayerAction = true, 
                    AvailableActions = new[] { "Check", "Bet", "Fold", "AllIn" } 
                },
                new() 
                { 
                    PhaseId = "SixthStreet", 
                    Name = "Sixth Street", 
                    Description = "1 up card dealt", 
                    Category = "Betting", 
                    RequiresPlayerAction = true, 
                    AvailableActions = new[] { "Check", "Bet", "Fold", "AllIn" } 
                },
                new() 
                { 
                    PhaseId = "SeventhStreet", 
                    Name = "Seventh Street", 
                    Description = "1 down card dealt", 
                    Category = "Betting", 
                    RequiresPlayerAction = true, 
                    AvailableActions = new[] { "Check", "Bet", "Fold", "AllIn" } 
                },
                new() 
                { 
                    PhaseId = "Showdown", 
                    Name = "Showdown", 
                    Description = "Comparing hands to determine winner", 
                    Category = "Resolution", 
                    RequiresPlayerAction = false 
                },
                new() 
                { 
                    PhaseId = "Complete", 
                    Name = "Complete", 
                    Description = "Hand finished, awarding pot", 
                    Category = "Resolution", 
                    RequiresPlayerAction = false ,
                    IsTerminal = true
                }
            },
            CardDealing = new CardDealingConfig
            {
                InitialCards = 3,
                InitialVisibility = CardVisibility.Mixed,
                HasCommunityCards = false,
                DealingRounds = new List<DealingRound>
                {
                    new() { CardCount = 2, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    new() { CardCount = 1, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players }
                }
            },
            Betting = new BettingConfig
            {
                HasAntes = true,
                HasBlinds = false,
                BettingRounds = 5,
                Structure = "Fixed Limit"
            },
            Showdown = new ShowdownConfig
            {
                HandRanking = "Follow the Queen",
                IsHighLow = false,
                HasSpecialSplitRules = false
            },
            SpecialRules = new Dictionary<string, object>
            {
                ["WildCards"] = "Queens + Next Card after face-up Queen",
                ["WildCardRule"] = "Queens + Next Card",
                ["HasBringIn"] = true
            }
        };
    }
}
