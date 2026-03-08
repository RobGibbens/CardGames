using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.HoldTheBaseball;

/// <summary>
/// Orchestrates a Hold the Baseball poker game with betting.
/// 
/// Hold the Baseball is identical to Texas Hold 'Em in gameplay,
/// except that 3s and 9s are wild (including community cards).
/// </summary>
[PokerGameMetadata(
	code:"HOLDTHEBASEBALL",
	name:"Hold the Baseball",
	description:"A Texas Hold 'Em variant where 3s and 9s are wild, including community cards.",
	minimumNumberOfPlayers:2,
	maximumNumberOfPlayers:10,
	initialHoleCards:2,
	initialBoardCards:0,
	maxCommunityCards:5,
	maxPlayerCards:2,
	hasDrawPhase:false,
	maxDiscards:0,
	wildCardRule:WildCardRule.FixedRanks,
	bettingStructure:BettingStructure.Blinds,
	imageName:"holdthebaseball.png")]
public class HoldTheBaseballGame : IPokerGame
{
	public string Name { get; } = "Hold the Baseball";
	public string Description { get; } = "A Texas Hold 'Em variant where 3s and 9s are wild, including community cards.";
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 10;

	private readonly List<HoldTheBaseballGamePlayer> _gamePlayers;
	private readonly FrenchDeckDealer _dealer;
	private readonly int _smallBlind;
	private readonly int _bigBlind;

	private PotManager _potManager;
	private BettingRound _currentBettingRound;
	private int _dealerPosition;
	private List<Card> _communityCards = [];

	public Phases CurrentPhase { get; private set; }
	public IReadOnlyList<HoldTheBaseballGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
	public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
	public int TotalPot => _potManager?.TotalPotAmount ?? 0;
	public BettingRound CurrentBettingRound => _currentBettingRound;
	public int DealerPosition => _dealerPosition;
	public PotManager PotManager => _potManager;
	public int SmallBlind => _smallBlind;
	public int BigBlind => _bigBlind;
	public IReadOnlyList<Card> CommunityCards => _communityCards.AsReadOnly();

	/// <summary>
	/// Constructor for rules discovery only.
	/// </summary>
	public HoldTheBaseballGame()
		: this(
			new[] { ("P1", 100), ("P2", 100) },
			smallBlind: 0,
			bigBlind: 0)
	{
	}

	public HoldTheBaseballGame(
		IEnumerable<(string name, int chips)> players,
		int smallBlind,
		int bigBlind)
	{
		var playerList = players.ToList();
		if (playerList.Count < MinimumNumberOfPlayers)
		{
			throw new ArgumentException($"Hold the Baseball requires at least {MinimumNumberOfPlayers} players");
		}

		if (playerList.Count > MaximumNumberOfPlayers)
		{
			throw new ArgumentException($"Hold the Baseball supports at most {MaximumNumberOfPlayers} players");
		}

		_gamePlayers = playerList
			.Select(p => new HoldTheBaseballGamePlayer(new PokerPlayer(p.name, p.chips)))
			.ToList();

		_dealer = FrenchDeckDealer.WithFullDeck();
		_smallBlind = smallBlind;
		_bigBlind = bigBlind;
		_dealerPosition = 0;
		CurrentPhase = Phases.WaitingToStart;
	}

	/// <inheritdoc />
	public GameFlow.GameRules GetGameRules()
	{
		return HoldTheBaseballRules.CreateGameRules();
	}

	public void StartHand()
	{
		_dealer.Shuffle();
		_potManager = new PotManager();
		_communityCards = [];

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetForNewHand();
			gamePlayer.ResetHand();
		}

		CurrentPhase = Phases.CollectingBlinds;
	}

	public int GetSmallBlindPosition()
	{
		if (_gamePlayers.Count == 2)
		{
			return _dealerPosition;
		}
		return (_dealerPosition + 1) % _gamePlayers.Count;
	}

	public int GetBigBlindPosition()
	{
		if (_gamePlayers.Count == 2)
		{
			return (_dealerPosition + 1) % _gamePlayers.Count;
		}
		return (_dealerPosition + 2) % _gamePlayers.Count;
	}

	private int GetFirstToActPreFlop()
	{
		if (_gamePlayers.Count == 2)
		{
			return _dealerPosition;
		}
		return (GetBigBlindPosition() + 1) % _gamePlayers.Count;
	}

	private int GetFirstToActPostFlop()
	{
		var position = (_dealerPosition + 1) % _gamePlayers.Count;
		var count = 0;
		while (count < _gamePlayers.Count)
		{
			if (!_gamePlayers[position].Player.HasFolded)
			{
				return position;
			}
			position = (position + 1) % _gamePlayers.Count;
			count++;
		}
		return _dealerPosition;
	}

	public List<BettingAction> CollectBlinds()
	{
		if (CurrentPhase != Phases.CollectingBlinds)
		{
			throw new InvalidOperationException("Cannot collect blinds in current phase");
		}

		var actions = new List<BettingAction>();

		var sbPosition = GetSmallBlindPosition();
		var sbPlayer = _gamePlayers[sbPosition].Player;
		var sbAmount = Math.Min(_smallBlind, sbPlayer.ChipStack);
		if (sbAmount > 0)
		{
			var actualSb = sbPlayer.PlaceBet(sbAmount);
			_potManager.AddContribution(sbPlayer.Name, actualSb);
			actions.Add(new BettingAction(sbPlayer.Name, BettingActionType.Post, actualSb));
		}

		var bbPosition = GetBigBlindPosition();
		var bbPlayer = _gamePlayers[bbPosition].Player;
		var bbAmount = Math.Min(_bigBlind, bbPlayer.ChipStack);
		if (bbAmount > 0)
		{
			var actualBb = bbPlayer.PlaceBet(bbAmount);
			_potManager.AddContribution(bbPlayer.Name, actualBb);
			actions.Add(new BettingAction(bbPlayer.Name, BettingActionType.Post, actualBb));
		}

		CurrentPhase = Phases.Dealing;
		return actions;
	}

	public void DealHoleCards()
	{
		if (CurrentPhase != Phases.Dealing)
		{
			throw new InvalidOperationException("Cannot deal in current phase");
		}

		foreach (var gamePlayer in _gamePlayers)
		{
			if (!gamePlayer.Player.HasFolded)
			{
				var cards = _dealer.DealCards(2);
				gamePlayer.SetHoleCards(cards);
			}
		}

		CurrentPhase = Phases.PreFlop;
	}

	public void StartPreFlopBettingRound()
	{
		if (CurrentPhase != Phases.PreFlop)
		{
			throw new InvalidOperationException("Cannot start pre-flop betting in current phase");
		}

		var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
		var firstToAct = GetFirstToActPreFlop();
		var virtualDealerPosition = (firstToAct - 1 + _gamePlayers.Count) % _gamePlayers.Count;
		var bbPosition = GetBigBlindPosition();
		var initialBet = _gamePlayers[bbPosition].Player.CurrentBet;

		_currentBettingRound = new BettingRound(
			activePlayers,
			_potManager,
			virtualDealerPosition,
			_bigBlind,
			initialBet,
			bbPosition);
	}

	public void DealFlop()
	{
		if (CurrentPhase != Phases.Flop)
		{
			throw new InvalidOperationException("Cannot deal flop in current phase");
		}

		var flopCards = _dealer.DealCards(3);
		_communityCards.AddRange(flopCards);
	}

	public void DealTurn()
	{
		if (CurrentPhase != Phases.Turn)
		{
			throw new InvalidOperationException("Cannot deal turn in current phase");
		}

		var turnCard = _dealer.DealCard();
		_communityCards.Add(turnCard);
	}

	public void DealRiver()
	{
		if (CurrentPhase != Phases.River)
		{
			throw new InvalidOperationException("Cannot deal river in current phase");
		}

		var riverCard = _dealer.DealCard();
		_communityCards.Add(riverCard);
	}

	public void StartPostFlopBettingRound()
	{
		if (CurrentPhase is not (Phases.Flop or Phases.Turn or Phases.River))
		{
			throw new InvalidOperationException("Cannot start post-flop betting in current phase");
		}

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetCurrentBet();
		}

		var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
		var firstToAct = GetFirstToActPostFlop();
		var virtualDealerPosition = (firstToAct - 1 + _gamePlayers.Count) % _gamePlayers.Count;

		_currentBettingRound = new BettingRound(
			activePlayers,
			_potManager,
			virtualDealerPosition,
			_bigBlind);
	}

	public AvailableActions GetAvailableActions()
	{
		return _currentBettingRound?.GetAvailableActions();
	}

	public PokerPlayer GetCurrentPlayer()
	{
		return _currentBettingRound?.CurrentPlayer;
	}

	public BettingRoundResult ProcessBettingAction(BettingActionType actionType, int amount = 0)
	{
		if (_currentBettingRound == null)
		{
			return new BettingRoundResult
			{
				Success = false,
				ErrorMessage = "No active betting round"
			};
		}

		var result = _currentBettingRound.ProcessAction(actionType, amount);

		if (result.RoundComplete)
		{
			AdvanceToNextPhase();
		}

		return result;
	}

	private void AdvanceToNextPhase()
	{
		var playersInHand = _gamePlayers.Count(gp => !gp.Player.HasFolded);
		if (playersInHand <= 1)
		{
			CurrentPhase = Phases.Showdown;
			return;
		}

		var hasAllIn = _gamePlayers.Any(gp => gp.Player.IsAllIn && !gp.Player.HasFolded);
		if (hasAllIn)
		{
			_potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
		}

		var activePlayersWhoCanAct = _gamePlayers.Count(gp => !gp.Player.HasFolded && !gp.Player.IsAllIn);
		if (activePlayersWhoCanAct == 0)
		{
			CurrentPhase = Phases.Showdown;
			return;
		}

		foreach (var gamePlayer in _gamePlayers)
		{
			gamePlayer.Player.ResetCurrentBet();
		}

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

	public HoldTheBaseballShowdownResult PerformShowdown()
	{
		if (CurrentPhase != Phases.Showdown)
		{
			return new HoldTheBaseballShowdownResult
			{
				Success = false,
				ErrorMessage = "Not in showdown phase"
			};
		}

		var playersInHand = _gamePlayers.Where(gp => !gp.Player.HasFolded).ToList();

		if (playersInHand.Count == 1)
		{
			var winner = playersInHand[0];
			var totalPot = _potManager.TotalPotAmount;
			winner.Player.AddChips(totalPot);

			CurrentPhase = Phases.Complete;
			MoveDealer();

			return new HoldTheBaseballShowdownResult
			{
				Success = true,
				Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
				PlayerHands = new Dictionary<string, (HoldTheBaseballHand hand, IReadOnlyCollection<Card> holeCards)>
				{
					{ winner.Player.Name, (null, winner.HoleCards) }
				},
				WonByFold = true
			};
		}

		while (_communityCards.Count < 5)
		{
			_communityCards.Add(_dealer.DealCard());
		}

		var playerHands = playersInHand.ToDictionary(
			gp => gp.Player.Name,
			gp => (hand: new HoldTheBaseballHand(gp.HoleCards, _communityCards), holeCards: (IReadOnlyCollection<Card>)gp.HoleCards)
		);

		var payouts = _potManager.AwardPots(eligiblePlayers =>
		{
			var eligibleHands = playerHands
				.Where(kvp => eligiblePlayers.Contains(kvp.Key))
				.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.hand);

			var maxStrength = eligibleHands.Values.Max(h => h.Strength);
			return eligibleHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key);
		});

		foreach (var payout in payouts)
		{
			var gamePlayer = _gamePlayers.First(gp => gp.Player.Name == payout.Key);
			gamePlayer.Player.AddChips(payout.Value);
		}

		CurrentPhase = Phases.Complete;
		MoveDealer();

		return new HoldTheBaseballShowdownResult
		{
			Success = true,
			Payouts = payouts,
			PlayerHands = playerHands
		};
	}

	private void MoveDealer()
	{
		_dealerPosition = (_dealerPosition + 1) % _gamePlayers.Count;
	}

	public IEnumerable<PokerPlayer> GetPlayersWithChips()
	{
		return _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);
	}

	public bool CanContinue()
	{
		return GetPlayersWithChips().Count() >= 2;
	}

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

	public HoldTheBaseballGamePlayer GetDealer()
	{
		return _gamePlayers[_dealerPosition];
	}

	public HoldTheBaseballGamePlayer GetSmallBlindPlayer()
	{
		return _gamePlayers[GetSmallBlindPosition()];
	}

	public HoldTheBaseballGamePlayer GetBigBlindPlayer()
	{
		return _gamePlayers[GetBigBlindPosition()];
	}
}
