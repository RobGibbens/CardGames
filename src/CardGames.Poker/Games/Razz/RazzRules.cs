using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Razz;

public static class RazzRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "RAZZ",
            GameTypeName = "Razz",
            Description = "Seven Card Stud lowball. Lowest five-card hand wins, aces are low, and straights/flushes do not count against low.",
            MinPlayers = 2,
            MaxPlayers = 7,
            Phases =
            [
                new GamePhaseDescriptor
                {
                    PhaseId = "WaitingToStart",
                    Name = "Waiting to Start",
                    Description = "Waiting for players to join and ready up",
                    Category = "Setup",
                    RequiresPlayerAction = false
                },
                new GamePhaseDescriptor
                {
                    PhaseId = "CollectingAntes",
                    Name = "Collecting Antes",
                    Description = "Collecting ante bets from all players",
                    Category = "Setup",
                    RequiresPlayerAction = false
                },
                new GamePhaseDescriptor
                {
                    PhaseId = "ThirdStreet",
                    Name = "Third Street",
                    Description = "2 down cards and 1 up card dealt, bring-in betting round",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = ["Call", "Raise", "Fold", "AllIn"]
                },
                new GamePhaseDescriptor
                {
                    PhaseId = "FourthStreet",
                    Name = "Fourth Street",
                    Description = "1 up card dealt, betting round with small bet",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
                },
                new GamePhaseDescriptor
                {
                    PhaseId = "FifthStreet",
                    Name = "Fifth Street",
                    Description = "1 up card dealt, betting round with big bet",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
                },
                new GamePhaseDescriptor
                {
                    PhaseId = "SixthStreet",
                    Name = "Sixth Street",
                    Description = "1 up card dealt, betting round with big bet",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
                },
                new GamePhaseDescriptor
                {
                    PhaseId = "SeventhStreet",
                    Name = "Seventh Street",
                    Description = "1 down card dealt, final betting round with big bet",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
                },
                new GamePhaseDescriptor
                {
                    PhaseId = "Showdown",
                    Name = "Showdown",
                    Description = "Players reveal and the lowest hand wins",
                    Category = "Resolution",
                    RequiresPlayerAction = false
                },
                new GamePhaseDescriptor
                {
                    PhaseId = "Complete",
                    Name = "Complete",
                    Description = "Hand is complete",
                    Category = "Resolution",
                    RequiresPlayerAction = false,
                    IsTerminal = true
                }
            ],
            CardDealing = new CardDealingConfig
            {
                InitialCards = 3,
                InitialVisibility = CardVisibility.Mixed,
                HasCommunityCards = false,
                DealingRounds =
                [
                    new DealingRound { CardCount = 2, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
                    new DealingRound { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    new DealingRound { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    new DealingRound { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    new DealingRound { CardCount = 1, Visibility = CardVisibility.FaceUp, Target = DealingTarget.Players },
                    new DealingRound { CardCount = 1, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players }
                ]
            },
            Betting = new BettingConfig
            {
                HasAntes = true,
                HasBlinds = false,
                BettingRounds = 5,
                Structure = "Fixed Limit"
            },
            Drawing = null,
            Showdown = new ShowdownConfig
            {
                HandRanking = "Ace-to-Five Lowball",
                IsHighLow = false,
                HasSpecialSplitRules = false,
                SpecialSplitDescription = "Lowest five-card hand always wins. Straights and flushes are ignored for low evaluation."
            },
            SpecialRules = new Dictionary<string, object>
            {
                ["AcesLow"] = true,
                ["StraightsAndFlushesIgnored"] = true,
                ["HasQualifier"] = false,
                ["HasBringIn"] = true,
                ["BringInOnLowestCard"] = true,
                ["MaxPlayerCards"] = 7
            }
        };
    }
}
