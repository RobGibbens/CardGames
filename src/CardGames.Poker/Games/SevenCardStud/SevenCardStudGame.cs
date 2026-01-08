using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Games.SevenCardStud;

/// <summary>
/// Orchestrates a Seven Card Stud poker game with betting.
/// </summary>
[PokerGameMetadata(
	"Seven Card Stud",
	"A classic poker variant where players receive seven cards individually, with betting rounds after the third, fourth, fifth, and sixth cards.",
	2,
	7,
	"sevencardstud.png")]
public class SevenCardStudGame : IPokerGame
{
	public string Name { get; } = "Seven Card Stud";
	public string Description { get; } = "A classic poker variant where players receive seven cards individually, with betting rounds after the third, fourth, fifth, and sixth cards.";
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 7;

	private readonly List<SevenCardStudGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _ante;
    private readonly int _bringIn;
    private readonly int _smallBet;
    private readonly int _bigBet;
    private readonly bool _useBringIn;

    private PotManager _potManager;
    private BettingRound _currentBettingRound;
    private int _dealerPosition;
    private int _bringInPlayerIndex;

    public SevenCardStudPhase CurrentPhase { get; private set; }
    public IReadOnlyList<SevenCardStudGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public int TotalPot => _potManager?.TotalPotAmount ?? 0;
    public BettingRound CurrentBettingRound => _currentBettingRound;
    public int DealerPosition => _dealerPosition;
    public PotManager PotManager => _potManager;
    public int Ante => _ante;
    public int BringIn => _bringIn;
    public int SmallBet => _smallBet;
    public int BigBet => _bigBet;
    public bool UseBringIn => _useBringIn;

    public SevenCardStudGame(
        IEnumerable<(string name, int chips)> players,
        int ante,
        int bringIn,
        int smallBet,
        int bigBet,
        bool useBringIn = false)
    {
        var playerList = players.ToList();
        if (playerList.Count < MinimumNumberOfPlayers)
        {
            throw new ArgumentException($"Seven Card Stud requires at least {MinimumNumberOfPlayers} players");
        }

        if (playerList.Count > MaximumNumberOfPlayers)
        {
            throw new ArgumentException($"Seven Card Stud supports at most {MaximumNumberOfPlayers} players");
        }

        _gamePlayers = playerList
            .Select(p => new SevenCardStudGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _dealer = FrenchDeckDealer.WithFullDeck();
        _ante = ante;
        _bringIn = bringIn;
        _smallBet = smallBet;
        _bigBet = bigBet;
        _useBringIn = useBringIn;
        _dealerPosition = 0;
        CurrentPhase = SevenCardStudPhase.WaitingToStart;
    }

    /// <summary>
    /// Gets the game rules metadata for Seven Card Stud.
    /// </summary>
    public GameFlow.GameRules GetGameRules()
    {
        return SevenCardStudRules.CreateGameRules();
    }

    /// <summary>
    /// Starts a new hand.
    /// </summary>
    public void StartHand()
    {
        _dealer.Shuffle();
        _potManager = new PotManager();

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetForNewHand();
            gamePlayer.ResetHand();
        }

        CurrentPhase = SevenCardStudPhase.CollectingAntes;
    }

    /// <summary>
    /// Collects antes from all players.
    /// </summary>
    public List<BettingAction> CollectAntes()
    {
        if (CurrentPhase != SevenCardStudPhase.CollectingAntes)
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

        CurrentPhase = SevenCardStudPhase.ThirdStreet;
        return actions;
    }

    /// <summary>
    /// Deals the third street cards (2 hole cards + 1 board card) to all players.
    /// </summary>
    public void DealThirdStreet()
    {
        if (CurrentPhase != SevenCardStudPhase.ThirdStreet)
        {
            throw new InvalidOperationException("Cannot deal third street in current phase");
        }

        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                // Deal 2 hole cards (face down)
                gamePlayer.AddHoleCard(_dealer.DealCard());
                gamePlayer.AddHoleCard(_dealer.DealCard());
                // Deal 1 board card (face up)
                gamePlayer.AddBoardCard(_dealer.DealCard());
            }
        }

        // Reset current bets before betting round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        // Determine the bring-in player (lowest upcard) only if bring-in is enabled
        if (_useBringIn)
        {
            _bringInPlayerIndex = FindBringInPlayer();
        }
        else
        {
            _bringInPlayerIndex = -1;
        }
    }

    /// <summary>
    /// Posts the bring-in bet for third street.
    /// Returns the bring-in action.
    /// Throws if bring-in is disabled or not in the correct phase.
    /// </summary>
    public BettingAction PostBringIn()
    {
        if (CurrentPhase != SevenCardStudPhase.ThirdStreet)
        {
            throw new InvalidOperationException("Cannot post bring-in in current phase");
        }

        if (!_useBringIn)
        {
            throw new InvalidOperationException("Bring-in is disabled for this game");
        }

        var bringInPlayer = _gamePlayers[_bringInPlayerIndex];
        var actualAmount = bringInPlayer.Player.PlaceBet(Math.Min(_bringIn, bringInPlayer.Player.ChipStack));
        _potManager.AddContribution(bringInPlayer.Player.Name, actualAmount);

        return new BettingAction(bringInPlayer.Player.Name, BettingActionType.Post, actualAmount);
    }

    /// <summary>
    /// Starts the betting round for the current street.
    /// For third street with bring-in, starts after the bring-in player.
    /// For third street without bring-in, starts with best visible hand (like other streets).
    /// For other streets, starts with best visible hand.
    /// </summary>
    public void StartBettingRound()
    {
        var activePlayers = _gamePlayers.Select(gp => gp.Player).ToList();
        int startPosition;
        int minBet;
        int initialBet = 0;
        int forcedBetPlayerIndex = -1;

        switch (CurrentPhase)
        {
            case SevenCardStudPhase.ThirdStreet:
                if (_useBringIn && _bringInPlayerIndex >= 0)
                {
                    startPosition = _bringInPlayerIndex;
                    // The bring-in creates an initial bet
                    var bringInPlayer = _gamePlayers[_bringInPlayerIndex].Player;
                    initialBet = bringInPlayer.CurrentBet;
                    forcedBetPlayerIndex = _bringInPlayerIndex;
                }
                else
                {
                    // No bring-in: start with best visible hand
                    startPosition = GetDealerPositionForFirstActingPlayer(FindBestVisibleHandPosition());
                }
                minBet = _smallBet;
                break;
            case SevenCardStudPhase.FourthStreet:
                startPosition = GetDealerPositionForFirstActingPlayer(FindBestVisibleHandPosition());
                minBet = _smallBet;
                break;
            case SevenCardStudPhase.FifthStreet:
            case SevenCardStudPhase.SixthStreet:
            case SevenCardStudPhase.SeventhStreet:
                startPosition = GetDealerPositionForFirstActingPlayer(FindBestVisibleHandPosition());
                minBet = _bigBet;
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
    /// Gets the bring-in player for third street.
    /// </summary>
    public SevenCardStudGamePlayer GetBringInPlayer()
    {
        if (_bringInPlayerIndex < 0 || _bringInPlayerIndex >= _gamePlayers.Count)
        {
            return null;
        }
        return _gamePlayers[_bringInPlayerIndex];
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
            CurrentPhase = SevenCardStudPhase.Showdown;
            return;
        }

        // Reset current bets for next round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        switch (CurrentPhase)
        {
            case SevenCardStudPhase.ThirdStreet:
                CurrentPhase = SevenCardStudPhase.FourthStreet;
                break;
            case SevenCardStudPhase.FourthStreet:
                CurrentPhase = SevenCardStudPhase.FifthStreet;
                break;
            case SevenCardStudPhase.FifthStreet:
                CurrentPhase = SevenCardStudPhase.SixthStreet;
                break;
            case SevenCardStudPhase.SixthStreet:
                CurrentPhase = SevenCardStudPhase.SeventhStreet;
                break;
            case SevenCardStudPhase.SeventhStreet:
                _potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
                CurrentPhase = SevenCardStudPhase.Showdown;
                break;
        }
    }

    /// <summary>
    /// Deals one card for the current street (4th-7th street).
    /// </summary>
    public void DealStreetCard()
    {
        if (CurrentPhase is not (SevenCardStudPhase.FourthStreet
            or SevenCardStudPhase.FifthStreet
            or SevenCardStudPhase.SixthStreet
            or SevenCardStudPhase.SeventhStreet))
        {
            throw new InvalidOperationException("Cannot deal street card in current phase");
        }

        foreach (var gamePlayer in _gamePlayers)
        {
            if (!gamePlayer.Player.HasFolded)
            {
                if (CurrentPhase == SevenCardStudPhase.SeventhStreet)
                {
                    // Seventh street card is dealt face down
                    gamePlayer.AddHoleCard(_dealer.DealCard());
                }
                else
                {
                    // 4th, 5th, 6th street cards are dealt face up
                    gamePlayer.AddBoardCard(_dealer.DealCard());
                }
            }
        }

        // Reset current bets before betting round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }
    }

    /// <summary>
    /// Performs the showdown and determines winners.
    /// </summary>
    public SevenCardStudShowdownResult PerformShowdown()
    {
        if (CurrentPhase != SevenCardStudPhase.Showdown)
        {
            return new SevenCardStudShowdownResult
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

            CurrentPhase = SevenCardStudPhase.Complete;
            MoveDealer();

            return new SevenCardStudShowdownResult
            {
                Success = true,
                Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
                PlayerHands = new Dictionary<string, (SevenCardStudHand hand, IReadOnlyCollection<Card> cards)>
                {
                    { winner.Player.Name, (null, winner.AllCards.ToList()) }
                },
                WonByFold = true
            };
        }

        // Evaluate hands - create StudHand from hole cards and board cards
        var playerHands = playersInHand.ToDictionary(
            gp => gp.Player.Name,
            gp =>
            {
                var holeCards = gp.HoleCards.Take(2).ToList();
                var boardCards = gp.BoardCards.ToList();
                // The 7th street card is dealt face down and added to hole cards
                var downCard = gp.HoleCards.Count > 2 ? gp.HoleCards[2] : holeCards.LastOrDefault();
                
                // Create a StudHand for evaluation
                var allCards = gp.HoleCards.Concat(gp.BoardCards).ToList();
                var hand = new SevenCardStudHand(
                    holeCards, 
                    boardCards, 
                    downCard);
                    
                return (hand, cards: (IReadOnlyCollection<Card>)allCards);
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

        CurrentPhase = SevenCardStudPhase.Complete;
        MoveDealer();

        return new SevenCardStudShowdownResult
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
    /// Finds the player with the lowest upcard (for bring-in).
    /// In case of tie, use suit order: clubs (lowest), diamonds, hearts, spades (highest).
    /// </summary>
    private int FindBringInPlayer()
    {
        int lowestIndex = -1;
        Card lowestCard = null;

        for (int i = 0; i < _gamePlayers.Count; i++)
        {
            var gamePlayer = _gamePlayers[i];
            if (gamePlayer.Player.HasFolded || gamePlayer.BoardCards.Count == 0)
            {
                continue;
            }

            var upcard = gamePlayer.BoardCards[0];
            
            // Use 'is null' pattern because Card's == operator is overloaded
            if (lowestCard is null || CompareCardsForBringIn(upcard, lowestCard) < 0)
            {
                lowestCard = upcard;
                lowestIndex = i;
            }
        }

        return lowestIndex;
    }

    /// <summary>
    /// Compares two cards for bring-in determination.
    /// Lower value is "worse". For ties, use suit order (clubs lowest, spades highest).
    /// </summary>
    private static int CompareCardsForBringIn(Card a, Card b)
    {
        if (a.Value != b.Value)
        {
            return a.Value.CompareTo(b.Value);
        }

        // Suit order for ties: Clubs (0) < Diamonds (1) < Hearts (2) < Spades (3)
        return GetSuitRank(a.Suit).CompareTo(GetSuitRank(b.Suit));
    }

    private static int GetSuitRank(Suit suit)
    {
        return suit switch
        {
            Suit.Clubs => 0,
            Suit.Diamonds => 1,
            Suit.Hearts => 2,
            Suit.Spades => 3,
            _ => 0
        };
    }

    /// <summary>
    /// Finds the position of the player with the best visible hand (for betting order).
    /// </summary>
    private int FindBestVisibleHandPosition()
    {
        int bestIndex = -1;
        long bestStrength = -1;

        for (int i = 0; i < _gamePlayers.Count; i++)
        {
            var gamePlayer = _gamePlayers[i];
            if (gamePlayer.Player.HasFolded || gamePlayer.BoardCards.Count == 0)
            {
                continue;
            }

            var visibleStrength = EvaluateVisibleHand(gamePlayer.BoardCards);
            
            if (visibleStrength > bestStrength)
            {
                bestStrength = visibleStrength;
                bestIndex = i;
            }
        }

        return bestIndex >= 0 ? bestIndex : 0;
    }

    /// <summary>
    /// Evaluates the strength of visible cards for determining betting order.
    /// Higher is better (pairs, high cards, etc.)
    /// </summary>
    private static long EvaluateVisibleHand(IReadOnlyCollection<Card> boardCards)
    {
        if (boardCards.Count == 0) return 0;

        var cards = boardCards.OrderByDescending(c => c.Value).ToList();
        var valueCounts = cards.GroupBy(c => c.Value)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .ToList();

        long strength = 0;
        
        // Check for pairs, trips, etc.
        var maxCount = valueCounts.First().Count();
        
        if (maxCount >= 4)
        {
            // Four of a kind
            strength = 7_000_000 + valueCounts.First().Key * 1000;
        }
        else if (maxCount >= 3)
        {
            // Three of a kind
            strength = 4_000_000 + valueCounts.First().Key * 1000;
        }
        else if (maxCount >= 2)
        {
            var pairs = valueCounts.Where(g => g.Count() >= 2).ToList();
            if (pairs.Count >= 2)
            {
                // Two pair
                strength = 3_000_000 + pairs[0].Key * 1000 + pairs[1].Key * 10;
            }
            else
            {
                // One pair
                strength = 2_000_000 + pairs[0].Key * 1000;
            }
        }
        else
        {
            // High card(s)
            strength = 1_000_000;
        }

        // Add kicker values
        foreach (var card in cards.Take(4))
        {
            strength = strength * 15 + card.Value;
        }

        return strength;
    }

    /// <summary>
    /// Gets the current minimum bet for this street.
    /// </summary>
    public int GetCurrentMinBet()
    {
        return CurrentPhase switch
        {
            SevenCardStudPhase.ThirdStreet => _smallBet,
            SevenCardStudPhase.FourthStreet => _smallBet,
            SevenCardStudPhase.FifthStreet => _bigBet,
            SevenCardStudPhase.SixthStreet => _bigBet,
            SevenCardStudPhase.SeventhStreet => _bigBet,
            _ => _smallBet
        };
    }

    /// <summary>
    /// Gets the street name for display purposes.
    /// </summary>
    public string GetCurrentStreetName()
    {
        return CurrentPhase switch
        {
            SevenCardStudPhase.ThirdStreet => "Third Street",
            SevenCardStudPhase.FourthStreet => "Fourth Street",
            SevenCardStudPhase.FifthStreet => "Fifth Street",
            SevenCardStudPhase.SixthStreet => "Sixth Street",
            SevenCardStudPhase.SeventhStreet => "Seventh Street (River)",
            _ => CurrentPhase.ToString()
        };
    }
}