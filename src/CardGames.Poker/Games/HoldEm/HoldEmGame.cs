using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.HoldEm;

/// <summary>
///     Orchestrates a Texas Hold 'Em poker game with betting.
///     Texas Hold 'Em uses a dealer button, small blind, and big blind structure.
///     Each hand has four betting rounds: pre-flop, flop, turn, and river.
/// </summary>
[PokerGameMetadata(
	"HOLDEM",
	"Texas Hold 'Em",
	"A popular variant of poker where players are dealt two hole cards and share five community cards, with four betting rounds.",
	2,
	14,
	2,
	0,
	5,
	2,
	false,
	0,
	WildCardRule.None,
	BettingStructure.Blinds,
	"holdem.png")]
public class HoldEmGame : IPokerGame
{
	private readonly FrenchDeckDealer _dealer;

	private readonly List<HoldEmGamePlayer> _gamePlayers;
	private List<Card> _communityCards = [];

	/// <summary>
	///     Constructor for rules discovery only.
	/// </summary>
	public HoldEmGame()
		: this(
			new[] { ("P1", 100), ("P2", 100) },
			0,
			0)
	{
	}

	public HoldEmGame(
		IEnumerable<(string name, int chips)> players,
		int smallBlind,
		int bigBlind)
	{
		var playerList = players.ToList();
		if (playerList.Count < MinimumNumberOfPlayers)
			throw new ArgumentException($"Hold 'Em requires at least {MinimumNumberOfPlayers} players");

		if (playerList.Count > MaximumNumberOfPlayers)
			throw new ArgumentException($"Hold 'Em supports at most {MaximumNumberOfPlayers} players");

		_gamePlayers =
		[
			.. playerList
				.Select(p => new HoldEmGamePlayer(new PokerPlayer(p.name, p.chips)))
		];

		_dealer = FrenchDeckDealer.WithFullDeck();
		SmallBlind = smallBlind;
		BigBlind = bigBlind;
		DealerPosition = 0;
		CurrentPhase = Phases.WaitingToStart;
	}

	public Phases CurrentPhase { get; private set; }
	public IReadOnlyList<HoldEmGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
	public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
	public int TotalPot => PotManager?.TotalPotAmount ?? 0;
	public BettingRound CurrentBettingRound { get; private set; }

	public int DealerPosition { get; private set; }

	public PotManager PotManager { get; private set; }

	public int SmallBlind { get; }

	public int BigBlind { get; }

	public IReadOnlyList<Card> CommunityCards => _communityCards.AsReadOnly();
	public string Name { get; } = "Texas Hold 'Em";

	public string Description { get; } =
		"A popular variant of poker where players are dealt two hole cards and share five community cards, with four betting rounds.";

	public VariantType VariantType { get; } = VariantType.HoldEm;
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	/// <summary>
	///     Gets the game rules metadata for Hold'em.
	/// </summary>
	public GameRules GetGameRules()
	{
		return new GameRules
		{
			GameTypeCode = "HOLDEM",
			GameTypeName = "Texas Hold 'Em",
			Description =
				"A popular variant of poker where players are dealt two hole cards and share five community cards, with four betting rounds.",
			MinPlayers = 2,
			MaxPlayers = 10,
			Phases = new List<GamePhaseDescriptor>
			{
				new()
				{
					PhaseId = "WaitingToStart", Name = "Waiting to Start", Description = "Waiting for players",
					Category = "Setup", RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "PreFlop", Name = "Pre-Flop", Description = "Initial betting round", Category = "Betting",
					RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "Flop", Name = "Flop", Description = "First 3 community cards", Category = "Betting",
					RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "Turn", Name = "Turn", Description = "4th community card", Category = "Betting",
					RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "River", Name = "River", Description = "5th community card", Category = "Betting",
					RequiresPlayerAction = true
				},
				new()
				{
					PhaseId = "Showdown", Name = "Showdown", Description = "Determine winner", Category = "Resolution",
					RequiresPlayerAction = false
				},
				new()
				{
					PhaseId = "Complete", Name = "Complete", Description = "Hand complete", Category = "Resolution",
					RequiresPlayerAction = false, IsTerminal = true
				}
			},
			CardDealing = new CardDealingConfig
			{
				InitialCards = 2,
				InitialVisibility = CardVisibility.FaceDown,
				HasCommunityCards = true,
				DealingRounds = new List<DealingRound>
				{
					new() { CardCount = 2, Visibility = CardVisibility.FaceDown, Target = DealingTarget.Players },
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
			Showdown = new ShowdownConfig
			{
				HandRanking = "Standard",
				IsHighLow = false,
				HasSpecialSplitRules = false
			}
		};
	}

	/// <summary>
	///     Starts a new hand.
	/// </summary>
	public void StartHand()
	{
		_dealer.Shuffle();
		PotManager = new PotManager();
		_communityCards = [];

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetForNewHand();
			gamePlayer.ResetHand();
		}

		CurrentPhase = Phases.CollectingBlinds;
	}

	/// <summary>
	///     Gets the small blind player position.
	///     For heads-up (2 players), the dealer is the small blind.
	///     For 3+ players, the player to the left of the dealer is the small blind.
	/// </summary>
	public int GetSmallBlindPosition()
	{
		if (_gamePlayers.Count == 2)
			// Heads-up: dealer is small blind
			return DealerPosition;

		return (DealerPosition + 1) % _gamePlayers.Count;
	}

	/// <summary>
	///     Gets the big blind player position.
	///     For heads-up (2 players), the non-dealer is the big blind.
	///     For 3+ players, the player two positions left of the dealer is the big blind.
	/// </summary>
	public int GetBigBlindPosition()
	{
		if (_gamePlayers.Count == 2)
			// Heads-up: non-dealer is big blind
			return (DealerPosition + 1) % _gamePlayers.Count;

		return (DealerPosition + 2) % _gamePlayers.Count;
	}

	/// <summary>
	///     Gets the position that acts first pre-flop.
	///     For heads-up (2 players), the dealer (small blind) acts first.
	///     For 3+ players, the player left of the big blind acts first.
	/// </summary>
	private int GetFirstToActPreFlop()
	{
		if (_gamePlayers.Count == 2)
			// Heads-up: dealer (small blind) acts first pre-flop
			return DealerPosition;

		return (GetBigBlindPosition() + 1) % _gamePlayers.Count;
	}

	/// <summary>
	///     Gets the position that acts first post-flop.
	///     The first active player to the left of the dealer acts first.
	/// </summary>
	private int GetFirstToActPostFlop()
	{
		var position = (DealerPosition + 1) % _gamePlayers.Count;
		var count = 0;
		while (count < _gamePlayers.Count)
		{
			if (!_gamePlayers[position].Player.HasFolded) return position;

			position = (position + 1) % _gamePlayers.Count;
			count++;
		}

		return DealerPosition;
	}

	/// <summary>
	///     Collects blinds from the small blind and big blind players.
	/// </summary>
	public List<BettingAction> CollectBlinds()
	{
		if (CurrentPhase != Phases.CollectingBlinds)
			throw new InvalidOperationException("Cannot collect blinds in current phase");

		var actions = new List<BettingAction>();

		// Post small blind
		var sbPosition = GetSmallBlindPosition();
		var sbPlayer = _gamePlayers[sbPosition].Player;
		var sbAmount = Math.Min(SmallBlind, sbPlayer.ChipStack);
		if (sbAmount > 0)
		{
			var actualSb = sbPlayer.PlaceBet(sbAmount);
			PotManager.AddContribution(sbPlayer.Name, actualSb);
			actions.Add(new BettingAction(sbPlayer.Name, BettingActionType.Post, actualSb));
		}

		// Post big blind
		var bbPosition = GetBigBlindPosition();
		var bbPlayer = _gamePlayers[bbPosition].Player;
		var bbAmount = Math.Min(BigBlind, bbPlayer.ChipStack);
		if (bbAmount > 0)
		{
			var actualBb = bbPlayer.PlaceBet(bbAmount);
			PotManager.AddContribution(bbPlayer.Name, actualBb);
			actions.Add(new BettingAction(bbPlayer.Name, BettingActionType.Post, actualBb));
		}

		CurrentPhase = Phases.Dealing;
		return actions;
	}

	/// <summary>
	///     Deals hole cards to all players.
	/// </summary>
	public void DealHoleCards()
	{
		if (CurrentPhase != Phases.Dealing) throw new InvalidOperationException("Cannot deal in current phase");

		// Deal 2 cards to each player
		foreach (var gamePlayer in _gamePlayers)
			if (!gamePlayer.Player.HasFolded)
			{
				var cards = _dealer.DealCards(2);
				gamePlayer.SetHoleCards(cards);
			}

		CurrentPhase = Phases.PreFlop;
	}

	/// <summary>
	///     Starts the pre-flop betting round.
	/// </summary>
	public void StartPreFlopBettingRound()
	{
		if (CurrentPhase != Phases.PreFlop)
			throw new InvalidOperationException("Cannot start pre-flop betting in current phase");

		var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
		var firstToAct = GetFirstToActPreFlop();

		// Convert first-to-act position to dealer position for BettingRound
		// BettingRound starts with (dealerPosition + 1) % playerCount
		var virtualDealerPosition = (firstToAct - 1 + _gamePlayers.Count) % _gamePlayers.Count;

		// The big blind is the initial bet
		var bbPosition = GetBigBlindPosition();
		var initialBet = _gamePlayers[bbPosition].Player.CurrentBet;

		CurrentBettingRound = new BettingRound(
			activePlayers,
			PotManager,
			virtualDealerPosition,
			BigBlind,
			initialBet,
			bbPosition);
	}

	/// <summary>
	///     Deals the flop (3 community cards).
	/// </summary>
	public void DealFlop()
	{
		if (CurrentPhase != Phases.Flop) throw new InvalidOperationException("Cannot deal flop in current phase");

		var flopCards = _dealer.DealCards(3);
		_communityCards.AddRange(flopCards);
	}

	/// <summary>
	///     Deals the turn (4th community card).
	/// </summary>
	public void DealTurn()
	{
		if (CurrentPhase != Phases.Turn) throw new InvalidOperationException("Cannot deal turn in current phase");

		var turnCard = _dealer.DealCard();
		_communityCards.Add(turnCard);
	}

	/// <summary>
	///     Deals the river (5th community card).
	/// </summary>
	public void DealRiver()
	{
		if (CurrentPhase != Phases.River) throw new InvalidOperationException("Cannot deal river in current phase");

		var riverCard = _dealer.DealCard();
		_communityCards.Add(riverCard);
	}

	/// <summary>
	///     Starts a post-flop betting round (flop, turn, or river).
	/// </summary>
	public void StartPostFlopBettingRound()
	{
		if (CurrentPhase is not (Phases.Flop or Phases.Turn or Phases.River))
			throw new InvalidOperationException("Cannot start post-flop betting in current phase");

		// Reset current bets for the new round
		foreach (var gamePlayer in _gamePlayers) gamePlayer.Player.ResetCurrentBet();

		var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
		var firstToAct = GetFirstToActPostFlop();

		// Convert first-to-act position to dealer position for BettingRound
		var virtualDealerPosition = (firstToAct - 1 + _gamePlayers.Count) % _gamePlayers.Count;

		CurrentBettingRound = new BettingRound(
			activePlayers,
			PotManager,
			virtualDealerPosition,
			BigBlind);
	}

	/// <summary>
	///     Gets the available actions for the current player.
	/// </summary>
	public AvailableActions GetAvailableActions()
	{
		return CurrentBettingRound?.GetAvailableActions();
	}

	/// <summary>
	///     Gets the current player who needs to act.
	/// </summary>
	public PokerPlayer GetCurrentPlayer()
	{
		return CurrentBettingRound?.CurrentPlayer;
	}

	/// <summary>
	///     Processes a betting action from the current player.
	/// </summary>
	public BettingRoundResult ProcessBettingAction(BettingActionType actionType, int amount = 0)
	{
		if (CurrentBettingRound == null)
			return new BettingRoundResult
			{
				Success = false,
				ErrorMessage = "No active betting round"
			};

		var result = CurrentBettingRound.ProcessAction(actionType, amount);

		if (result.RoundComplete) AdvanceToNextPhase();

		return result;
	}

	private void AdvanceToNextPhase()
	{
		// Check if only one player remains
		var playersInHand = _gamePlayers.Count(gp => !gp.Player.HasFolded);
		if (playersInHand <= 1)
		{
			CurrentPhase = Phases.Showdown;
			return;
		}

		// Calculate side pots if any players are all-in
		var hasAllIn = _gamePlayers.Any(gp => gp.Player.IsAllIn && !gp.Player.HasFolded);
		if (hasAllIn) PotManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));

		// Check if all remaining players are all-in (run out the board)
		var activePlayersWhoCanAct = _gamePlayers.Count(gp => !gp.Player.HasFolded && !gp.Player.IsAllIn);
		if (activePlayersWhoCanAct == 0)
		{
			// All remaining players are all-in - skip to showdown
			CurrentPhase = Phases.Showdown;
			return;
		}

		// Reset current bets for next round
		foreach (var gamePlayer in _gamePlayers) gamePlayer.Player.ResetCurrentBet();

		switch (CurrentPhase)
		{
			case Phases.PreFlop:
				CurrentPhase = Phases.Flop;
				break;
			case Phases.Flop:
				CurrentPhase = Phases.Turn;
				break;
			case Phases.Turn:
				CurrentPhase = Phases.River;
				break;
			case Phases.River:
				CurrentPhase = Phases.Showdown;
				break;
		}
	}

	/// <summary>
	///     Performs the showdown and determines winners.
	/// </summary>
	public HoldEmShowdownResult PerformShowdown()
	{
		if (CurrentPhase != Phases.Showdown)
			return new HoldEmShowdownResult
			{
				Success = false,
				ErrorMessage = "Not in showdown phase"
			};

		var playersInHand = _gamePlayers.Where(gp => !gp.Player.HasFolded).ToList();

		// If only one player remains, they win by default
		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];
			var totalPot = PotManager.TotalPotAmount;
			winner.Player.AddChips(totalPot);

			CurrentPhase = Phases.Complete;
			MoveDealer();

			return new HoldEmShowdownResult
			{
				Success = true,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				PlayerHands = new Dictionary<string, (HoldemHand hand, IReadOnlyCollection<Card> holeCards)>
				{
					{ winner.Player.Name, (null, winner.HoleCards) }
				},
				WonByFold = true
			};
		}

		// Deal remaining community cards if needed (when everyone is all-in)
		while (_communityCards.Count < 5) _communityCards.Add(_dealer.DealCard());

		// Evaluate hands
		var playerHands = playersInHand.ToDictionary(
			gp => gp.Player.Name,
			gp => (hand: new HoldemHand(gp.HoleCards, _communityCards),
				holeCards: (IReadOnlyCollection<Card>)gp.HoleCards)
		);

		// Award pots
		var payouts = PotManager.AwardPots(eligiblePlayers =>
		{
			var eligibleHands = playerHands
				.Where(kvp => eligiblePlayers.Contains(kvp.Key))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.hand);

			var maxStrength = eligibleHands.Values.Max(h => h.Strength);
			return eligibleHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key);
		});

		// Add winnings to player stacks
		foreach (var payout in payouts)
		{
			var gamePlayer = _gamePlayers.First(gp => gp.Player.Name == payout.Key);
			gamePlayer.Player.AddChips(payout.Value);
		}

		CurrentPhase = Phases.Complete;
		MoveDealer();

		return new HoldEmShowdownResult
		{
			Success = true,
			Payouts = payouts,
			PlayerHands = playerHands
		};
	}

	private void MoveDealer()
	{
		DealerPosition = (DealerPosition + 1) % _gamePlayers.Count;
	}

	/// <summary>
	///     Gets the players who can continue playing (have chips).
	/// </summary>
	public IEnumerable<PokerPlayer> GetPlayersWithChips()
	{
		return _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);
	}

	/// <summary>
	///     Checks if the game can continue (at least 2 players have chips).
	/// </summary>
	public bool CanContinue()
	{
		return GetPlayersWithChips().Count() >= 2;
	}

	/// <summary>
	///     Gets the current street name for display.
	/// </summary>
	public string GetCurrentStreetName()
	{
		return CurrentPhase switch
		{
			Phases.PreFlop => "Pre-Flop",
			Phases.Flop => "Flop",
			Phases.Turn => "Turn",
			Phases.River => "River",
			_ => CurrentPhase.ToString()
		};
	}

	/// <summary>
	///     Gets the dealer player.
	/// </summary>
	public HoldEmGamePlayer GetDealer()
	{
		return _gamePlayers[DealerPosition];
	}

	/// <summary>
	///     Gets the small blind player.
	/// </summary>
	public HoldEmGamePlayer GetSmallBlindPlayer()
	{
		return _gamePlayers[GetSmallBlindPosition()];
	}

	/// <summary>
	///     Gets the big blind player.
	/// </summary>
	public HoldEmGamePlayer GetBigBlindPlayer()
	{
		return _gamePlayers[GetBigBlindPosition()];
	}
}