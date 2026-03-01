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
            Description = "Each player receives four hole cards and shares three community cards. The Good sets wild cards, The Bad forces discards, and The Ugly creates dead hands that can still bet.",
            MinPlayers = 2,
            MaxPlayers = 10,
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
                    Name = "Initial Betting",
                    Description = "After each player is dealt 4 face-down cards and 3 community cards are dealt face-down, betting starts left of dealer",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
                },
                new()
                {
                    PhaseId = "RevealTheGood",
                    Name = "Reveal The Good",
                    Description = "First community card is revealed — matching ranks become wild",
                    Category = "Special",
                    RequiresPlayerAction = false
                },
                new()
                {
                    PhaseId = "FourthStreet",
                    Name = "Betting After The Good",
                    Description = "Betting round after The Good reveal",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
                },
                new()
                {
                    PhaseId = "RevealTheBad",
                    Name = "Reveal The Bad",
                    Description = "Second community card is revealed — matching cards in hand are discarded",
                    Category = "Special",
                    RequiresPlayerAction = false
                },
                new()
                {
                    PhaseId = "FifthStreet",
                    Name = "Betting After The Bad",
                    Description = "Betting round after The Bad reveal",
                    Category = "Betting",
                    RequiresPlayerAction = true,
                    AvailableActions = new[] { "Check", "Bet", "Call", "Raise", "Fold", "AllIn" }
                },
                new()
                {
                    PhaseId = "RevealTheUgly",
                    Name = "Reveal The Ugly",
                    Description = "Third community card is revealed — players with matching cards have dead hands but may still bet",
                    Category = "Special",
                    RequiresPlayerAction = false
                },
                new()
                {
                    PhaseId = "SixthStreet",
                    Name = "Final Betting",
                    Description = "Final betting round; players eliminated by The Ugly may still bet to buy the pot",
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
                InitialCards = 4,
                InitialVisibility = CardVisibility.FaceDown,
                HasCommunityCards = true,
                DealingRounds = new List<DealingRound>
                {
                    new() { CardCount = 4, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
                    new() { CardCount = 3, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Community }
                }
            },
            Betting = new BettingConfig
            {
                HasAntes = true,
                HasBlinds = false,
                BettingRounds = 4,
                Structure = "Fixed Limit"
            },
            Drawing = null,
            Showdown = new ShowdownConfig
            {
                HandRanking = "Standard Poker (High) with Wild Cards",
                IsHighLow = false,
                HasSpecialSplitRules = true
            },
            SpecialRules = new Dictionary<string, object>
            {
                ["HasBringIn"] = false,
                ["BringInOnLowestCard"] = false,
                ["MaxPlayerCards"] = 4,
                ["TableCards"] = 3,
                ["TheGood"] = "First community card revealed after the first betting round — matching ranks become wild",
                ["TheBad"] = "Second community card revealed after the second betting round — matching cards in hand are discarded",
                ["TheUgly"] = "Third community card revealed after the third betting round — matching hands are dead but can still bet",
                ["WildCards"] = "Dynamic — determined by The Good table card",
                ["AllEliminatedSplitRule"] = true
            }
        };
    }
}
