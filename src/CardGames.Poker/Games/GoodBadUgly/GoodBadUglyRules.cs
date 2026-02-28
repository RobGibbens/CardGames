using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.GoodBadUgly;

/// <summary>
/// Provides game rules metadata for The Good, the Bad, and the Ugly.
/// </summary>
public static class GoodBadUglyRules
{
    public static GameRules CreateGameRules()
    {
        return new GameRules
        {
            GameTypeCode = "GOODBADUGLY",
            GameTypeName = "The Good, the Bad, and the Ugly",
            Description = "Seven Card Stud with three table cards. 'The Good' makes a rank wild, 'The Bad' forces discards of matching cards, and 'The Ugly' eliminates players with matching face-up cards.",
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
                    Description = "1 up card dealt, betting round with small bet",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
                },
                new()
                {
                    PhaseId = "RevealTheGood",
                    Name = "Reveal The Good",
                    Description = "First table card is revealed — matching ranks become wild",
                    Category = "Special",
                    RequiresPlayerAction = false
                },
                new()
                {
                    PhaseId = "FifthStreet",
                    Name = "Fifth Street",
                    Description = "1 up card dealt, betting round with big bet",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
                },
                new()
                {
                    PhaseId = "RevealTheBad",
                    Name = "Reveal The Bad",
                    Description = "Second table card is revealed — matching cards in any hand must be discarded",
                    Category = "Special",
                    RequiresPlayerAction = false
                },
                new()
                {
                    PhaseId = "SixthStreet",
                    Name = "Sixth Street",
                    Description = "1 up card dealt, betting round with big bet",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
                },
                new()
                {
                    PhaseId = "RevealTheUgly",
                    Name = "Reveal The Ugly",
                    Description = "Third table card is revealed — players with matching face-up cards are eliminated",
                    Category = "Special",
                    RequiresPlayerAction = false
                },
                new()
                {
                    PhaseId = "SeventhStreet",
                    Name = "Seventh Street (River)",
                    Description = "1 down card dealt, final betting round with big bet",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
                },
                new()
                {
                    PhaseId = "Showdown",
                    Name = "Showdown",
                    Description = "Players reveal hands and winner is determined (wild cards applied from The Good)",
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
            Drawing = null,
            Showdown = new ShowdownConfig
            {
                HandRanking = "Standard Poker (High) with Wild Cards",
                IsHighLow = false,
                HasSpecialSplitRules = false
            },
            SpecialRules = new Dictionary<string, object>
            {
                ["HasBringIn"] = true,
                ["BringInOnLowestCard"] = true,
                ["MaxPlayerCards"] = 7,
                ["TableCards"] = 3,
                ["TheGood"] = "First table card revealed after 4th street — matching ranks become wild",
                ["TheBad"] = "Second table card revealed after 5th street — matching cards must be discarded",
                ["TheUgly"] = "Third table card revealed after 6th street — players with matching face-up cards are eliminated",
                ["WildCards"] = "Dynamic — determined by The Good table card",
                ["SuitOrderForBringIn"] = "Clubs, Diamonds, Hearts, Spades (lowest to highest)"
            }
        };
    }
}
