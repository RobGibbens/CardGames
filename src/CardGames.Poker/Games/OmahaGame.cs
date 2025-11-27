using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.CommunityCardHands;

namespace CardGames.Poker.Games;

/// <summary>
/// Represents a player in an Omaha game with their cards and betting state.
/// </summary>
public class OmahaGamePlayer
{
    public PokerPlayer Player { get; }
    public List<Card> HoleCards { get; private set; } = new List<Card>();

    public OmahaGamePlayer(PokerPlayer player)
    {
        Player = player;
    }

    public void AddHoleCard(Card card)
    {
        HoleCards.Add(card);
    }

    public void ResetHand()
    {
        HoleCards.Clear();
    }
}

/// <summary>
/// Orchestrates an Omaha poker game with betting.
/// Uses blinds (small blind and big blind) instead of antes.
/// </summary>
public class OmahaGame
{
    private const int MinPlayers = 2;
    private const int MaxPlayers = 10;
    private const int HoleCardsCount = 4;

    private readonly List<OmahaGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _smallBlind;
    private readonly int _bigBlind;

    private PotManager _potManager;
    private BettingRound _currentBettingRound;
    private int _dealerPosition;
    private List<Card> _communityCards = new List<Card>();

    public OmahaPhase CurrentPhase { get; private set; }
    public IReadOnlyList<OmahaGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public IReadOnlyList<Card> CommunityCards => _communityCards.AsReadOnly();
    public int TotalPot => _potManager?.TotalPotAmount ?? 0;
    public BettingRound CurrentBettingRound => _currentBettingRound;
    public int DealerPosition => _dealerPosition;
    public PotManager PotManager => _potManager;
    public int SmallBlind => _smallBlind;
    public int BigBlind => _bigBlind;

    public OmahaGame(
        IEnumerable<(string name, int chips)> players,
        int smallBlind,
        int bigBlind)
    {
        var playerList = players.ToList();
        if (playerList.Count < MinPlayers)
        {
            throw new ArgumentException($"Omaha requires at least {MinPlayers} players");
        }

        if (playerList.Count > MaxPlayers)
        {
            throw new ArgumentException($"Omaha supports at most {MaxPlayers} players");
        }

        _gamePlayers = playerList
            .Select(p => new OmahaGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _dealer = FrenchDeckDealer.WithFullDeck();
        _smallBlind = smallBlind;
        _bigBlind = bigBlind;
        _dealerPosition = 0;
        CurrentPhase = OmahaPhase.WaitingToStart;
    }

    /// <summary>
    /// Starts a new hand.
    /// </summary>
    public void StartHand()
    {
        _dealer.Shuffle();
        _potManager = new PotManager();
        _communityCards.Clear();

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetForNewHand();
            gamePlayer.ResetHand();
        }

        CurrentPhase = OmahaPhase.PostingBlinds;
    }

    /// <summary>
    /// Gets the small blind player position (left of dealer).
    /// </summary>
    public int SmallBlindPosition => (_dealerPosition + 1) % _gamePlayers.Count;

    /// <summary>
    /// Gets the big blind player position (left of small blind).
    /// </summary>
    public int BigBlindPosition => (_dealerPosition + 2) % _gamePlayers.Count;

    /// <summary>
    /// Collects blinds from players.
    /// </summary>
    public List<BettingAction> PostBlinds()
    {
        if (CurrentPhase != OmahaPhase.PostingBlinds)
        {
            throw new InvalidOperationException("Cannot post blinds in current phase");
        }

        var actions = new List<BettingAction>();

        // Small blind
        var sbPlayer = _gamePlayers[SmallBlindPosition];
        var sbAmount = Math.Min(_smallBlind, sbPlayer.Player.ChipStack);
        if (sbAmount > 0)
        {
            var actualSb = sbPlayer.Player.PlaceBet(sbAmount);
            _potManager.AddContribution(sbPlayer.Player.Name, actualSb);
            actions.Add(new BettingAction(sbPlayer.Player.Name, BettingActionType.Post, actualSb));
        }

        // Big blind
        var bbPlayer = _gamePlayers[BigBlindPosition];
        var bbAmount = Math.Min(_bigBlind, bbPlayer.Player.ChipStack);
        if (bbAmount > 0)
        {
            var actualBb = bbPlayer.Player.PlaceBet(bbAmount);
            _potManager.AddContribution(bbPlayer.Player.Name, actualBb);
            actions.Add(new BettingAction(bbPlayer.Player.Name, BettingActionType.Post, actualBb));
        }

        CurrentPhase = OmahaPhase.Preflop;
        return actions;
    }

    /// <summary>
    /// Deals 4 hole cards to each player.
    /// </summary>
    public void DealHoleCards()
    {
        if (CurrentPhase != OmahaPhase.Preflop)
        {
            throw new InvalidOperationException("Cannot deal hole cards in current phase");
        }

        // Deal cards one at a time, rotating around the table
        for (int cardNum = 0; cardNum < HoleCardsCount; cardNum++)
        {
            for (int i = 0; i < _gamePlayers.Count; i++)
            {
                var playerIndex = (SmallBlindPosition + i) % _gamePlayers.Count;
                var gamePlayer = _gamePlayers[playerIndex];
                if (!gamePlayer.Player.HasFolded)
                {
                    gamePlayer.AddHoleCard(_dealer.DealCard());
                }
            }
        }
    }

    /// <summary>
    /// Starts the betting round for the current phase.
    /// Preflop: action starts left of big blind, big blind is forced bet.
    /// Postflop: action starts left of dealer.
    /// </summary>
    public void StartBettingRound()
    {
        var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();

        // Reset current bets for the round (except preflop where blinds are already posted)
        if (CurrentPhase != OmahaPhase.Preflop)
        {
            foreach (var gamePlayer in _gamePlayers)
            {
                gamePlayer.Player.ResetCurrentBet();
            }
        }

        int startPosition;
        int minBet;
        int initialBet = 0;
        int forcedBetPlayerIndex = -1;

        switch (CurrentPhase)
        {
            case OmahaPhase.Preflop:
                // For preflop, action starts with player left of big blind
                // Big blind is the current bet to match
                startPosition = GetDealerPositionForFirstActingPlayer((BigBlindPosition + 1) % _gamePlayers.Count);
                minBet = _bigBlind;
                initialBet = _bigBlind;
                forcedBetPlayerIndex = BigBlindPosition;
                break;
            case OmahaPhase.Flop:
            case OmahaPhase.Turn:
            case OmahaPhase.River:
                // For postflop, action starts with first active player left of dealer
                startPosition = _dealerPosition;
                minBet = _bigBlind;
                break;
            default:
                throw new InvalidOperationException("Cannot start betting round in current phase");
        }

        _currentBettingRound = new BettingRound(activePlayers, _potManager, startPosition, minBet, initialBet, forcedBetPlayerIndex);
    }

    /// <summary>
    /// Converts a first-acting player index to a dealer position.
    /// BettingRound calculates the first player as (dealerPosition + 1) % playerCount,
    /// so we need to subtract 1 (with wraparound) to make the first-acting player start.
    /// </summary>
    private int GetDealerPositionForFirstActingPlayer(int firstActingPlayerIndex)
    {
        return (firstActingPlayerIndex - 1 + _gamePlayers.Count) % _gamePlayers.Count;
    }

    /// <summary>
    /// Gets the available actions for the current player in a betting round.
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
            CurrentPhase = OmahaPhase.Showdown;
            return;
        }

        // Reset current bets for next round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        switch (CurrentPhase)
        {
            case OmahaPhase.Preflop:
                CurrentPhase = OmahaPhase.Flop;
                break;
            case OmahaPhase.Flop:
                CurrentPhase = OmahaPhase.Turn;
                break;
            case OmahaPhase.Turn:
                CurrentPhase = OmahaPhase.River;
                break;
            case OmahaPhase.River:
                _potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
                CurrentPhase = OmahaPhase.Showdown;
                break;
        }
    }

    /// <summary>
    /// Deals the flop (3 community cards).
    /// </summary>
    public void DealFlop()
    {
        if (CurrentPhase != OmahaPhase.Flop)
        {
            throw new InvalidOperationException("Cannot deal flop in current phase");
        }

        _communityCards.Add(_dealer.DealCard());
        _communityCards.Add(_dealer.DealCard());
        _communityCards.Add(_dealer.DealCard());
    }

    /// <summary>
    /// Deals the turn (4th community card).
    /// </summary>
    public void DealTurn()
    {
        if (CurrentPhase != OmahaPhase.Turn)
        {
            throw new InvalidOperationException("Cannot deal turn in current phase");
        }

        _communityCards.Add(_dealer.DealCard());
    }

    /// <summary>
    /// Deals the river (5th community card).
    /// </summary>
    public void DealRiver()
    {
        if (CurrentPhase != OmahaPhase.River)
        {
            throw new InvalidOperationException("Cannot deal river in current phase");
        }

        _communityCards.Add(_dealer.DealCard());
    }

    /// <summary>
    /// Performs the showdown and determines winners.
    /// </summary>
    public OmahaShowdownResult PerformShowdown()
    {
        if (CurrentPhase != OmahaPhase.Showdown)
        {
            return new OmahaShowdownResult
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

            CurrentPhase = OmahaPhase.Complete;
            MoveDealer();

            return new OmahaShowdownResult
            {
                Success = true,
                Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
                PlayerHands = new Dictionary<string, (OmahaHand hand, IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)>
                {
                    { winner.Player.Name, (null, winner.HoleCards.ToList(), _communityCards.ToList()) }
                },
                WonByFold = true
            };
        }

        // Evaluate hands
        var playerHands = playersInHand.ToDictionary(
            gp => gp.Player.Name,
            gp =>
            {
                var hand = new OmahaHand(gp.HoleCards.ToList(), _communityCards.ToList());
                return (hand, holeCards: (IReadOnlyCollection<Card>)gp.HoleCards.ToList(), communityCards: (IReadOnlyCollection<Card>)_communityCards.ToList());
            }
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

        CurrentPhase = OmahaPhase.Complete;
        MoveDealer();

        return new OmahaShowdownResult
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
    /// Gets the current minimum bet for this street.
    /// </summary>
    public int GetCurrentMinBet()
    {
        return _bigBlind;
    }

    /// <summary>
    /// Gets the street name for display purposes.
    /// </summary>
    public string GetCurrentStreetName()
    {
        return CurrentPhase switch
        {
            OmahaPhase.Preflop => "Preflop",
            OmahaPhase.Flop => "Flop",
            OmahaPhase.Turn => "Turn",
            OmahaPhase.River => "River",
            _ => CurrentPhase.ToString()
        };
    }

    /// <summary>
    /// Gets the dealer player.
    /// </summary>
    public OmahaGamePlayer GetDealer()
    {
        return _gamePlayers[_dealerPosition];
    }

    /// <summary>
    /// Gets the small blind player.
    /// </summary>
    public OmahaGamePlayer GetSmallBlindPlayer()
    {
        return _gamePlayers[SmallBlindPosition];
    }

    /// <summary>
    /// Gets the big blind player.
    /// </summary>
    public OmahaGamePlayer GetBigBlindPlayer()
    {
        return _gamePlayers[BigBlindPosition];
    }
}

/// <summary>
/// Result of an Omaha showdown.
/// </summary>
public class OmahaShowdownResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; }
    public Dictionary<string, int> Payouts { get; init; }
    public Dictionary<string, (OmahaHand hand, IReadOnlyCollection<Card> holeCards, IReadOnlyCollection<Card> communityCards)> PlayerHands { get; init; }
    public bool WonByFold { get; init; }
}
