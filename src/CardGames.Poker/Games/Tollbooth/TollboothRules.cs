using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.Tollbooth;

/// <summary>
/// Provides game rules metadata for Tollbooth.
/// </summary>
public static class TollboothRules
{
	public static GameRules CreateGameRules()
	{
		return new GameRules
		{
			GameTypeCode = "TOLLBOOTH",
			GameTypeName = "Tollbooth",
			Description = "A Seven Card Stud variant where players choose their Fourth through Seventh street cards from a Tollbooth offer: the furthest display card (free), the nearest display card (1× ante), or the top deck card (2× ante).",
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
					AvailableActions = ["Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "TollboothOffer",
					Name = "Tollbooth Offer",
					Description = "Each player chooses a card: furthest display card (free), nearest display card (1× ante), or top deck card (2× ante)",
					Category = "Drawing",
					RequiresPlayerAction = true,
					AvailableActions = ["ChooseFurthest", "ChooseNearest", "ChooseDeck"]
				},
				new()
				{
					PhaseId = "FourthStreet",
					Name = "Fourth Street",
					Description = "Betting round after Tollbooth offer (small bet)",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "FifthStreet",
					Name = "Fifth Street",
					Description = "Betting round after Tollbooth offer (big bet)",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "SixthStreet",
					Name = "Sixth Street",
					Description = "Betting round after Tollbooth offer (big bet)",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
				},
				new()
				{
					PhaseId = "SeventhStreet",
					Name = "Seventh Street",
					Description = "Final betting round after Tollbooth offer (big bet)",
					Category = "Betting",
					RequiresPlayerAction = true,
					AvailableActions = ["Check", "Bet", "Call", "Raise", "Fold", "AllIn"]
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
				HandRanking = "Standard Poker (High)",
				IsHighLow = false,
				HasSpecialSplitRules = false
			},
			SpecialRules = new Dictionary<string, object>
			{
				["HasBringIn"] = true,
				["BringInOnLowestCard"] = true,
				["MaxPlayerCards"] = 7,
				["SuitOrderForBringIn"] = "Clubs, Diamonds, Hearts, Spades (lowest to highest)",
				["TollboothOffer"] = "After Third Street, each subsequent street's card is acquired via Tollbooth offer: furthest display card (free), nearest display card (1× ante), or deck card (2× ante).",
				["TollboothDisplayCards"] = "Two face-up cards on the table are visible to all but are NOT community cards for evaluation."
			}
		};
	}
}
