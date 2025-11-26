using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.DrawHands;

namespace CardGames.Poker.Games;

/// <summary>
/// Represents a player in a Five Card Draw game with their cards and betting state.
/// </summary>
public class FiveCardDrawGamePlayer
{
    public PokerPlayer Player { get; }
    public List<Card> Hand { get; private set; } = [];

    public FiveCardDrawGamePlayer(PokerPlayer player)
    {
        Player = player;
    }

    public void SetHand(IEnumerable<Card> cards)
    {
        Hand = cards.ToList();
    }

    public void DiscardAndDraw(IReadOnlyCollection<int> discardIndices, IReadOnlyCollection<Card> newCards)
    {
        // Remove cards at specified indices (in descending order to avoid index shifting)
        foreach (var index in discardIndices.OrderByDescending(i => i))
        {
            if (index >= 0 && index < Hand.Count)
            {
                Hand.RemoveAt(index);
            }
        }

        // Add new cards
        Hand.AddRange(newCards);
    }

    public void ResetHand()
    {
        Hand.Clear();
    }
}

/// <summary>
/// Orchestrates a Five Card Draw poker game with betting.
/// </summary>
public class FiveCardDrawGame
{
    private readonly List<FiveCardDrawGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _ante;
    private readonly int _minBet;

    private PotManager _potManager;
    private BettingRound _currentBettingRound;
    private int _dealerPosition;
    private int _currentDrawPlayerIndex;
    private HashSet<int> _playersWhoHaveDrawn;

    public FiveCardDrawPhase CurrentPhase { get; private set; }
    public IReadOnlyList<FiveCardDrawGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public int TotalPot => _potManager?.TotalPotAmount ?? 0;
    public BettingRound CurrentBettingRound => _currentBettingRound;
    public int DealerPosition => _dealerPosition;
    public PotManager PotManager => _potManager;
    public int Ante => _ante;

    public FiveCardDrawGame(IEnumerable<(string name, int chips)> players, int ante, int minBet)
    {
        var playerList = players.ToList();
        if (playerList.Count < 2)
        {
            throw new ArgumentException("Five Card Draw requires at least 2 players");
        }

        _gamePlayers = playerList
            .Select(p => new FiveCardDrawGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _dealer = FrenchDeckDealer.WithFullDeck();
        _ante = ante;
        _minBet = minBet;
        _dealerPosition = 0;
        CurrentPhase = FiveCardDrawPhase.WaitingToStart;
    }

    /// <summary>
    /// Starts a new hand.
    /// </summary>
    public void StartHand()
    {
        // Reset for new hand
        _dealer.Shuffle();
        _potManager = new PotManager();

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetForNewHand();
            gamePlayer.ResetHand();
        }

        CurrentPhase = FiveCardDrawPhase.CollectingAntes;
    }

    /// <summary>
    /// Collects antes from all players.
    /// </summary>
    public List<BettingAction> CollectAntes()
    {
        if (CurrentPhase != FiveCardDrawPhase.CollectingAntes)
        {
            throw new InvalidOperationException("Cannot collect antes in current phase");
        }

        var actions = new List<BettingAction>();

        foreach (var gamePlayer in _gamePlayers)
        {
            var player = gamePlayer.Player;
            var anteAmount = Math.Min(_ante, player.ChipStack);

            if (anteAmount > 0)
            {
                var actualAmount = player.PlaceBet(anteAmount);
                _potManager.AddContribution(player.Name, actualAmount);
                actions.Add(new BettingAction(player.Name, BettingActionType.Post, actualAmount));
            }
        }

        CurrentPhase = FiveCardDrawPhase.Dealing;
        return actions;
    }

    /// <summary>
    /// Deals initial hands to all players.
    /// </summary>
    public void DealHands()
    {
        if (CurrentPhase != FiveCardDrawPhase.Dealing)
        {
            throw new InvalidOperationException("Cannot deal in current phase");
        }

        // Deal 5 cards to each player
        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                var cards = _dealer.DealCards(5);
                gamePlayer.SetHand(cards);
            }
        }

        // Reset current bets before first betting round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        CurrentPhase = FiveCardDrawPhase.FirstBettingRound;
        StartBettingRound();
    }

    private void StartBettingRound()
    {
        var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
        _currentBettingRound = new BettingRound(activePlayers, _potManager, _dealerPosition, _minBet);
    }

    /// <summary>
    /// Gets the available actions for the current player in a betting round.
    /// </summary>
    public AvailableActions GetAvailableActions()
    {
        if (_currentBettingRound == null)
        {
            return null;
        }
        return _currentBettingRound.GetAvailableActions();
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
            CurrentPhase = FiveCardDrawPhase.Showdown;
            return;
        }

        // Reset current bets for next round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        switch (CurrentPhase)
        {
            case FiveCardDrawPhase.FirstBettingRound:
                CurrentPhase = FiveCardDrawPhase.DrawPhase;
                _currentDrawPlayerIndex = FindFirstActivePlayerAfterDealer();
                _playersWhoHaveDrawn = [];
                break;

            case FiveCardDrawPhase.SecondBettingRound:
                // Calculate side pots if needed
                _potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
                CurrentPhase = FiveCardDrawPhase.Showdown;
                break;
        }
    }

    private int FindFirstActivePlayerAfterDealer()
    {
        var index = (_dealerPosition + 1) % _gamePlayers.Count;
        var count = 0;
        while (count < _gamePlayers.Count)
        {
            if (!_gamePlayers[index].Player.HasFolded && !_gamePlayers[index].Player.IsAllIn)
            {
                return index;
            }
            index = (index + 1) % _gamePlayers.Count;
            count++;
        }
        return -1;
    }

    /// <summary>
    /// Gets the current player in the draw phase.
    /// </summary>
    public FiveCardDrawGamePlayer GetCurrentDrawPlayer()
    {
        if (CurrentPhase != FiveCardDrawPhase.DrawPhase || _currentDrawPlayerIndex < 0)
        {
            return null;
        }
        return _gamePlayers[_currentDrawPlayerIndex];
    }

    /// <summary>
    /// Processes a draw action (discard and draw cards) for the current player.
    /// </summary>
    public DrawResult ProcessDraw(IReadOnlyCollection<int> discardIndices)
    {
        if (CurrentPhase != FiveCardDrawPhase.DrawPhase)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Not in draw phase"
            };
        }

        var gamePlayer = _gamePlayers[_currentDrawPlayerIndex];

        if (discardIndices.Count > 3)
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Cannot discard more than 3 cards"
            };
        }

        if (discardIndices.Any(i => i < 0 || i >= 5))
        {
            return new DrawResult
            {
                Success = false,
                ErrorMessage = "Invalid card index (must be 0-4)"
            };
        }

        // Deal new cards
        var newCards = _dealer.DealCards(discardIndices.Count);
        var discardedCards = discardIndices.Select(i => gamePlayer.Hand[i]).ToList();
        gamePlayer.DiscardAndDraw(discardIndices, newCards);

        // Track that this player has drawn
        _playersWhoHaveDrawn.Add(_currentDrawPlayerIndex);

        // Move to next player or next phase
        MoveToNextDrawPlayer();

        return new DrawResult
        {
            Success = true,
            PlayerName = gamePlayer.Player.Name,
            DiscardedCards = discardedCards,
            NewCards = newCards.ToList(),
            DrawComplete = CurrentPhase != FiveCardDrawPhase.DrawPhase
        };
    }

    private void MoveToNextDrawPlayer()
    {
        // Find next eligible player who hasn't drawn yet
        var startIndex = _currentDrawPlayerIndex;
        _currentDrawPlayerIndex = (_currentDrawPlayerIndex + 1) % _gamePlayers.Count;
        var checkedCount = 0;

        while (checkedCount < _gamePlayers.Count)
        {
            var player = _gamePlayers[_currentDrawPlayerIndex].Player;
            
            // Check if this player can act and hasn't drawn yet
            if (!player.HasFolded && !player.IsAllIn && !_playersWhoHaveDrawn.Contains(_currentDrawPlayerIndex))
            {
                return;
            }

            _currentDrawPlayerIndex = (_currentDrawPlayerIndex + 1) % _gamePlayers.Count;
            checkedCount++;
        }

        // All players have drawn - move to second betting round
        StartSecondBettingRound();
    }

    private void StartSecondBettingRound()
    {
        CurrentPhase = FiveCardDrawPhase.SecondBettingRound;
        StartBettingRound();
    }

    /// <summary>
    /// Completes the draw phase (call when all players have drawn).
    /// </summary>
    public void CompleteDrawPhase()
    {
        if (CurrentPhase == FiveCardDrawPhase.DrawPhase)
        {
            StartSecondBettingRound();
        }
    }

    /// <summary>
    /// Performs the showdown and determines winners.
    /// </summary>
    public ShowdownResult PerformShowdown()
    {
        if (CurrentPhase != FiveCardDrawPhase.Showdown)
        {
            return new ShowdownResult
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

            CurrentPhase = FiveCardDrawPhase.Complete;
            MoveDealer();

            return new ShowdownResult
            {
                Success = true,
                Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
                PlayerHands = new Dictionary<string, (DrawHand hand, IReadOnlyCollection<Card> cards)>
                {
                    { winner.Player.Name, (null, winner.Hand) }
                },
                WonByFold = true
            };
        }

        // Evaluate hands
        var playerHands = playersInHand.ToDictionary(
            gp => gp.Player.Name,
            gp => (hand: new DrawHand(gp.Hand), cards: (IReadOnlyCollection<Card>)gp.Hand)
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

        CurrentPhase = FiveCardDrawPhase.Complete;
        MoveDealer();

        return new ShowdownResult
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
}

/// <summary>
/// Result of a draw action.
/// </summary>
public class DrawResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; }
    public string PlayerName { get; init; }
    public IReadOnlyCollection<Card> DiscardedCards { get; init; }
    public IReadOnlyCollection<Card> NewCards { get; init; }
    public bool DrawComplete { get; init; }
}

/// <summary>
/// Result of a showdown.
/// </summary>
public class ShowdownResult
{
    public bool Success { get; init; }
    public string ErrorMessage { get; init; }
    public Dictionary<string, int> Payouts { get; init; }
    public Dictionary<string, (DrawHand hand, IReadOnlyCollection<Card> cards)> PlayerHands { get; init; }
    public bool WonByFold { get; init; }
}
