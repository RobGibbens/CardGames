using System.Collections.Generic;
using CardGames.Poker.Games.GameFlow;

namespace CardGames.Poker.Games.InBetween;

public static class InBetweenRules
{
	public static GameRules CreateGameRules()
	{
		return new GameRules
		{
			GameTypeCode = "INBETWEEN",
			GameTypeName = "In-Between",
			Description = "Two boundary cards are dealt face-up. Players bet on whether the next card's rank falls strictly between the two boundaries. Match a boundary and you POST (pay double). Ace can be declared high or low. Game ends when the pot is empty.",
			MinPlayers = 2,
			MaxPlayers = 20,
			Phases =
			[
				new GamePhaseDescriptor
				{
					PhaseId = "WaitingToStart", Name = "Waiting to Start",
					Description = "Waiting for players", Category = "Setup",
					RequiresPlayerAction = false
				},
				new GamePhaseDescriptor
				{
					PhaseId = "CollectingAntes", Name = "Collecting Antes",
					Description = "Collecting antes from all players to build the pot",
					Category = "Setup", RequiresPlayerAction = false
				},
				new GamePhaseDescriptor
				{
					PhaseId = "InBetweenTurn", Name = "In-Between Turn",
					Description = "Active player is dealt two boundary cards and decides to bet or pass. If the first card is an Ace, the player must declare it high or low before the second boundary is dealt.",
					Category = "Decision", RequiresPlayerAction = true
				},
				new GamePhaseDescriptor
				{
					PhaseId = "Complete", Name = "Complete",
					Description = "Game complete — pot has been emptied",
					Category = "Resolution", RequiresPlayerAction = false, IsTerminal = true
				}
			],
			CardDealing = new CardDealingConfig
			{
				InitialCards = 0,
				InitialVisibility = CardVisibility.FaceUp,
				HasCommunityCards = false,
				DealingRounds =
				[
					new DealingRound
					{
						CardCount = 2, Visibility = CardVisibility.FaceUp,
						Target = DealingTarget.Community
					},
					new DealingRound
					{
						CardCount = 1, Visibility = CardVisibility.FaceUp,
						Target = DealingTarget.Community
					}
				]
			},
			Betting = new BettingConfig
			{
				HasAntes = true,
				HasBlinds = false,
				BettingRounds = 0,
				Structure = "Pot Limit"
			},
			Drawing = new DrawingConfig
			{
				AllowsDrawing = false,
				MaxDiscards = 0,
				SpecialRules = "No drawing. Cards are dealt to the table each turn."
			},
			Showdown = new ShowdownConfig
			{
				HandRanking = "None",
				IsHighLow = false,
				HasSpecialSplitRules = false
			},
			SpecialRules = new Dictionary<string, object>
			{
				["InBetween"] = true,
				["ContinuousDeck"] = true,
				["PostRule"] = true,
				["AceChoice"] = true,
				["FirstOrbitPotRestriction"] = true
			}
		};
	}
}
