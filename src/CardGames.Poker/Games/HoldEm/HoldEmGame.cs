using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games.HoldEm;

/// <summary>
/// Orchestrates a Texas Hold 'Em poker game with betting.
/// 
/// Texas Hold 'Em uses a dealer button, small blind, and big blind structure.
/// Each hand has four betting rounds: pre-flop, flop, turn, and river.
/// </summary>
[PokerGameMetadata(
	"Texas Hold 'Em",
	"A popular variant of poker where players are dealt two hole cards and share five community cards, with four betting rounds.",
	2,
	14)]
public class HoldEmGame : IPokerGame
{
	public string Name { get; } = "Texas Hold 'Em";
	public string Description { get; } = "A popular variant of poker where players are dealt two hole cards and share five community cards, with four betting rounds.";
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 14;

	private readonly List<HoldEmGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _smallBlind;
    private readonly int _bigBlind;

    private PotManager _potManager;
    private BettingRound _currentBettingRound;
    private int _dealerPosition;
    private List<Card> _communityCards = [];

    public HoldEmPhase CurrentPhase { get; private set; }
    public IReadOnlyList<HoldEmGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public int TotalPot => _potManager?.TotalPotAmount ?? 0;
    public BettingRound CurrentBettingRound => _currentBettingRound;
    public int DealerPosition => _dealerPosition;
    public PotManager PotManager => _potManager;
    public int SmallBlind => _smallBlind;
    public int BigBlind => _bigBlind;
    public IReadOnlyList<Card> CommunityCards => _communityCards.AsReadOnly();

    public HoldEmGame(
        IEnumerable<(string name, int chips)> players,
        int smallBlind,
        int bigBlind)
    {
        var playerList = players.ToList();
        if (playerList.Count < MinimumNumberOfPlayers)
        {
            throw new ArgumentException($"Hold 'Em requires at least {MinimumNumberOfPlayers} players");
        }

        if (playerList.Count > MaximumNumberOfPlayers)
        {
            throw new ArgumentException($"Hold 'Em supports at most {MaximumNumberOfPlayers} players");
        }

        _gamePlayers = playerList
            .Select(p => new HoldEmGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _dealer = FrenchDeckDealer.WithFullDeck();
        _smallBlind = smallBlind;
        _bigBlind = bigBlind;
        _dealerPosition = 0;
        CurrentPhase = HoldEmPhase.WaitingToStart;
    }

    /// <summary>
    /// Starts a new hand.
    /// </summary>
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

        CurrentPhase = HoldEmPhase.CollectingBlinds;
    }

    /// <summary>
    /// Gets the small blind player position.
    /// For heads-up (2 players), the dealer is the small blind.
    /// For 3+ players, the player to the left of the dealer is the small blind.
    /// </summary>
    public int GetSmallBlindPosition()
    {
        if (_gamePlayers.Count == 2)
        {
            // Heads-up: dealer is small blind
            return _dealerPosition;
        }
        return (_dealerPosition + 1) % _gamePlayers.Count;
    }

    /// <summary>
    /// Gets the big blind player position.
    /// For heads-up (2 players), the non-dealer is the big blind.
    /// For 3+ players, the player two positions left of the dealer is the big blind.
    /// </summary>
    public int GetBigBlindPosition()
    {
        if (_gamePlayers.Count == 2)
        {
            // Heads-up: non-dealer is big blind
            return (_dealerPosition + 1) % _gamePlayers.Count;
        }
        return (_dealerPosition + 2) % _gamePlayers.Count;
    }

    /// <summary>
    /// Gets the position that acts first pre-flop.
    /// For heads-up (2 players), the dealer (small blind) acts first.
    /// For 3+ players, the player left of the big blind acts first.
    /// </summary>
    private int GetFirstToActPreFlop()
    {
        if (_gamePlayers.Count == 2)
        {
            // Heads-up: dealer (small blind) acts first pre-flop
            return _dealerPosition;
        }
        return (GetBigBlindPosition() + 1) % _gamePlayers.Count;
    }

    /// <summary>
    /// Gets the position that acts first post-flop.
    /// The first active player to the left of the dealer acts first.
    /// </summary>
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

    /// <summary>
    /// Collects blinds from the small blind and big blind players.
    /// </summary>
    public List<BettingAction> CollectBlinds()
    {
        if (CurrentPhase != HoldEmPhase.CollectingBlinds)
        {
            throw new InvalidOperationException("Cannot collect blinds in current phase");
        }

        var actions = new List<BettingAction>();

        // Post small blind
        var sbPosition = GetSmallBlindPosition();
        var sbPlayer = _gamePlayers[sbPosition].Player;
        var sbAmount = Math.Min(_smallBlind, sbPlayer.ChipStack);
        if (sbAmount > 0)
        {
            var actualSb = sbPlayer.PlaceBet(sbAmount);
            _potManager.AddContribution(sbPlayer.Name, actualSb);
            actions.Add(new BettingAction(sbPlayer.Name, BettingActionType.Post, actualSb));
        }

        // Post big blind
        var bbPosition = GetBigBlindPosition();
        var bbPlayer = _gamePlayers[bbPosition].Player;
        var bbAmount = Math.Min(_bigBlind, bbPlayer.ChipStack);
        if (bbAmount > 0)
        {
            var actualBb = bbPlayer.PlaceBet(bbAmount);
            _potManager.AddContribution(bbPlayer.Name, actualBb);
            actions.Add(new BettingAction(bbPlayer.Name, BettingActionType.Post, actualBb));
        }

        CurrentPhase = HoldEmPhase.Dealing;
        return actions;
    }

    /// <summary>
    /// Deals hole cards to all players.
    /// </summary>
    public void DealHoleCards()
    {
        if (CurrentPhase != HoldEmPhase.Dealing)
        {
            throw new InvalidOperationException("Cannot deal in current phase");
        }

        // Deal 2 cards to each player
        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                var cards = _dealer.DealCards(2);
                gamePlayer.SetHoleCards(cards);
            }
        }

        CurrentPhase = HoldEmPhase.PreFlop;
    }

    /// <summary>
    /// Starts the pre-flop betting round.
    /// </summary>
    public void StartPreFlopBettingRound()
    {
        if (CurrentPhase != HoldEmPhase.PreFlop)
        {
            throw new InvalidOperationException("Cannot start pre-flop betting in current phase");
        }

        var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
        var firstToAct = GetFirstToActPreFlop();

        // Convert first-to-act position to dealer position for BettingRound
        // BettingRound starts with (dealerPosition + 1) % playerCount
        var virtualDealerPosition = (firstToAct - 1 + _gamePlayers.Count) % _gamePlayers.Count;

        // The big blind is the initial bet
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

    /// <summary>
    /// Deals the flop (3 community cards).
    /// </summary>
    public void DealFlop()
    {
        if (CurrentPhase != HoldEmPhase.Flop)
        {
            throw new InvalidOperationException("Cannot deal flop in current phase");
        }

        var flopCards = _dealer.DealCards(3);
        _communityCards.AddRange(flopCards);
    }

    /// <summary>
    /// Deals the turn (4th community card).
    /// </summary>
    public void DealTurn()
    {
        if (CurrentPhase != HoldEmPhase.Turn)
        {
            throw new InvalidOperationException("Cannot deal turn in current phase");
        }

        var turnCard = _dealer.DealCard();
        _communityCards.Add(turnCard);
    }

    /// <summary>
    /// Deals the river (5th community card).
    /// </summary>
    public void DealRiver()
    {
        if (CurrentPhase != HoldEmPhase.River)
        {
            throw new InvalidOperationException("Cannot deal river in current phase");
        }

        var riverCard = _dealer.DealCard();
        _communityCards.Add(riverCard);
    }

    /// <summary>
    /// Starts a post-flop betting round (flop, turn, or river).
    /// </summary>
    public void StartPostFlopBettingRound()
    {
        if (CurrentPhase is not (HoldEmPhase.Flop or HoldEmPhase.Turn or HoldEmPhase.River))
        {
            throw new InvalidOperationException("Cannot start post-flop betting in current phase");
        }

        // Reset current bets for the new round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
        var firstToAct = GetFirstToActPostFlop();

        // Convert first-to-act position to dealer position for BettingRound
        var virtualDealerPosition = (firstToAct - 1 + _gamePlayers.Count) % _gamePlayers.Count;

        _currentBettingRound = new BettingRound(
            activePlayers,
            _potManager,
            virtualDealerPosition,
            _bigBlind);
    }

    /// <summary>
    /// Gets the available actions for the current player.
    /// </summary>
    public AvailableActions GetAvailableActions()
    {
        return _currentBettingRound?.GetAvailableActions();
    }

    /// <summary>
    /// Gets the current player who needs to act.
    /// </summary>
    public PokerPlayer GetCurrentPlayer()
    {
        return _currentBettingRound?.CurrentPlayer;
    }

    /// <summary>
    /// Processes a betting action from the current player.
    /// </summary>
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
        // Check if only one player remains
        var playersInHand = _gamePlayers.Count(gp => !gp.Player.HasFolded);
        if (playersInHand <= 1)
        {
            CurrentPhase = HoldEmPhase.Showdown;
            return;
        }

        // Reset current bets for next round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        switch (CurrentPhase)
        {
            case HoldEmPhase.PreFlop:
                CurrentPhase = HoldEmPhase.Flop;
                break;
            case HoldEmPhase.Flop:
                CurrentPhase = HoldEmPhase.Turn;
                break;
            case HoldEmPhase.Turn:
                CurrentPhase = HoldEmPhase.River;
                break;
            case HoldEmPhase.River:
                _potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
                CurrentPhase = HoldEmPhase.Showdown;
                break;
        }
    }

    /// <summary>
    /// Performs the showdown and determines winners.
    /// </summary>
    public HoldEmShowdownResult PerformShowdown()
    {
        if (CurrentPhase != HoldEmPhase.Showdown)
        {
            return new HoldEmShowdownResult
            {
                Success = false,
                ErrorMessage = "Not in showdown phase"
            };
        }

        var playersInHand = _gamePlayers.Where(gp => !gp.Player.HasFolded).ToList();

        // If only one player remains, they win by default
        if (playersInHand.Count == 1)
        {
            var winner = playersInHand[0];
            var totalPot = _potManager.TotalPotAmount;
            winner.Player.AddChips(totalPot);

            CurrentPhase = HoldEmPhase.Complete;
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
        while (_communityCards.Count < 5)
        {
            _communityCards.Add(_dealer.DealCard());
        }

        // Evaluate hands
        var playerHands = playersInHand.ToDictionary(
            gp => gp.Player.Name,
            gp => (hand: new HoldemHand(gp.HoleCards, _communityCards), holeCards: (IReadOnlyCollection<Card>)gp.HoleCards)
        );

        // Award pots
        var payouts = _potManager.AwardPots(eligiblePlayers =>
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

        CurrentPhase = HoldEmPhase.Complete;
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
        _dealerPosition = (_dealerPosition + 1) % _gamePlayers.Count;
    }

    /// <summary>
    /// Gets the players who can continue playing (have chips).
    /// </summary>
    public IEnumerable<PokerPlayer> GetPlayersWithChips()
    {
        return _gamePlayers.Where(gp => gp.Player.ChipStack > 0).Select(gp => gp.Player);
    }

    /// <summary>
    /// Checks if the game can continue (at least 2 players have chips).
    /// </summary>
    public bool CanContinue()
    {
        return GetPlayersWithChips().Count() >= 2;
    }

    /// <summary>
    /// Gets the current street name for display.
    /// </summary>
    public string GetCurrentStreetName()
    {
        return CurrentPhase switch
        {
            HoldEmPhase.PreFlop => "Pre-Flop",
            HoldEmPhase.Flop => "Flop",
            HoldEmPhase.Turn => "Turn",
            HoldEmPhase.River => "River",
            _ => CurrentPhase.ToString()
        };
    }

    /// <summary>
    /// Gets the dealer player.
    /// </summary>
    public HoldEmGamePlayer GetDealer()
    {
        return _gamePlayers[_dealerPosition];
    }

    /// <summary>
    /// Gets the small blind player.
    /// </summary>
    public HoldEmGamePlayer GetSmallBlindPlayer()
    {
        return _gamePlayers[GetSmallBlindPosition()];
    }

    /// <summary>
    /// Gets the big blind player.
    /// </summary>
    public HoldEmGamePlayer GetBigBlindPlayer()
    {
        return _gamePlayers[GetBigBlindPosition()];
    }
}