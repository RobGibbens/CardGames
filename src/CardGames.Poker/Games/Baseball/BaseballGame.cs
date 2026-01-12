using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Betting;
using CardGames.Poker.Hands.StudHands;

namespace CardGames.Poker.Games.Baseball;

/// <summary>
/// Orchestrates a Baseball poker game with betting.
/// 
/// Baseball is a seven-card stud variant with special rules:
/// - 3s and 9s are wild
/// - When a player is dealt a 4 face up, they may pay the buy-card price
///   to receive an extra face-down card
/// - The buy-card payment goes to the pot but is NOT a bet
/// - Standard seven-card stud betting structure otherwise
/// </summary>
[PokerGameMetadata(
    code:"BASEBALL",
	name:"Baseball",
	description:"A seven-card stud variant with wild 3s and 9s, and buy-card options on 4s.",
 	minimumNumberOfPlayers: 2,
	maximumNumberOfPlayers: 6,
    initialHoleCards: 2,
    initialBoardCards: 1,
    maxCommunityCards: 0,
    maxPlayerCards: 7,
    hasDrawPhase: false,
    maxDiscards: 0,
    wildCardRule: WildCardRule.FixedRanks,
    bettingStructure: BettingStructure.AnteBringIn,
	imageName:"baseball.png")]
public class BaseballGame : IPokerGame
{
	public string Name { get; } = "Baseball";
	public string Description { get; } = "A seven-card stud variant with wild 3s and 9s, and buy-card options on 4s.";
	public int MinimumNumberOfPlayers { get; } = 2;
	public int MaximumNumberOfPlayers { get; } = 6;

    private readonly List<BaseballGamePlayer> _gamePlayers;
    private readonly FrenchDeckDealer _dealer;
    private readonly int _ante;
    private readonly int _bringIn;
    private readonly int _smallBet;
    private readonly int _bigBet;
    private readonly int _buyCardPrice;
    private readonly bool _useBringIn;

    private PotManager _potManager;
    private BettingRound _currentBettingRound;
    private int _dealerPosition;
    private int _bringInPlayerIndex;
    
    // For buy-card phase
    private Queue<(int playerIndex, int boardCardIndex)> _pendingBuyCardOffers = new();
    private (int playerIndex, int boardCardIndex)? _currentBuyCardOffer;
    
    // For tracking incremental dealing
    private int _nextPlayerToDeal = 0;

    public Phases CurrentPhase { get; private set; }
    public IReadOnlyList<BaseballGamePlayer> GamePlayers => _gamePlayers.AsReadOnly();
    public IReadOnlyList<PokerPlayer> Players => _gamePlayers.Select(gp => gp.Player).ToList().AsReadOnly();
    public int TotalPot => _potManager?.TotalPotAmount ?? 0;
    public BettingRound CurrentBettingRound => _currentBettingRound;
    public int DealerPosition => _dealerPosition;
    public PotManager PotManager => _potManager;
    public int Ante => _ante;
    public int BringIn => _bringIn;
    public int SmallBet => _smallBet;
    public int BigBet => _bigBet;
    public int BuyCardPrice => _buyCardPrice;
    public bool UseBringIn => _useBringIn;

    public BaseballGame(
        IEnumerable<(string name, int chips)> players,
        int ante,
        int bringIn,
        int smallBet,
        int bigBet,
        int buyCardPrice,
        bool useBringIn = false)
    {
        var playerList = players.ToList();
        if (playerList.Count < MinimumNumberOfPlayers)
        {
            throw new ArgumentException($"Baseball requires at least {MinimumNumberOfPlayers} players");
        }

        if (playerList.Count > MaximumNumberOfPlayers)
        {
            throw new ArgumentException($"Baseball supports at most {MaximumNumberOfPlayers} players");
        }

        _gamePlayers = playerList
            .Select(p => new BaseballGamePlayer(new PokerPlayer(p.name, p.chips)))
            .ToList();

        _dealer = FrenchDeckDealer.WithFullDeck();
        _ante = ante;
        _bringIn = bringIn;
        _smallBet = smallBet;
        _bigBet = bigBet;
        _buyCardPrice = buyCardPrice;
        _useBringIn = useBringIn;
        _dealerPosition = 0;
        CurrentPhase = Phases.WaitingToStart;
    }

    /// <summary>
    /// Gets the game rules metadata for Baseball.
    /// </summary>
    /// <remarks>
    /// Full implementation to be completed. For now, returns a placeholder.
    /// </remarks>
    public GameFlow.GameRules GetGameRules()
    {
        throw new NotImplementedException("Baseball game rules metadata not yet implemented");
    }

    /// <summary>
    /// Starts a new hand.
    /// </summary>
    public void StartHand()
    {
        _dealer.Shuffle();
        _potManager = new PotManager();
        _pendingBuyCardOffers.Clear();
        _currentBuyCardOffer = null;

        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetForNewHand();
            gamePlayer.ResetHand();
        }

        CurrentPhase = Phases.CollectingAntes;
    }

    /// <summary>
    /// Collects antes from all players.
    /// </summary>
    public List<BettingAction> CollectAntes()
    {
        if (CurrentPhase != Phases.CollectingAntes)
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

        CurrentPhase = Phases.ThirdStreet;
        return actions;
    }

    /// <summary>
    /// Deals the third street cards (2 hole cards + 1 board card) to all players at once.
    /// This method deals to all players before any buy-card offers are processed.
    /// For immediate buy-card processing (where each player's 4 offer is handled before 
    /// dealing to the next player), use StartDealingThirdStreet() and DealThirdStreetToNextPlayer() instead.
    /// </summary>
    public void DealThirdStreet()
    {
        if (CurrentPhase != Phases.ThirdStreet)
        {
            throw new InvalidOperationException("Cannot deal third street in current phase");
        }

        // Clear any existing offers and reset dealing state
        _pendingBuyCardOffers.Clear();
        _currentBuyCardOffer = null;
        _nextPlayerToDeal = 0;

        // Deal to all players
        while (HasMorePlayersToDeal())
        {
            DealThirdStreetToNextPlayer();
        }

        FinishThirdStreetDealing();
    }

    /// <summary>
    /// Starts the third street dealing process for incremental dealing.
    /// Use HasMorePlayersToDeal() and DealThirdStreetToNextPlayer() to deal one player at a time.
    /// </summary>
    public void StartDealingThirdStreet()
    {
        if (CurrentPhase != Phases.ThirdStreet)
        {
            throw new InvalidOperationException("Cannot deal third street in current phase");
        }

        // Clear any existing offers and reset dealing state
        _pendingBuyCardOffers.Clear();
        _currentBuyCardOffer = null;
        _nextPlayerToDeal = 0;
    }

    /// <summary>
    /// Checks if there are more players to deal to in the current dealing phase.
    /// </summary>
    public bool HasMorePlayersToDeal()
    {
        return _nextPlayerToDeal < _gamePlayers.Count;
    }

    /// <summary>
    /// Deals third street cards (2 hole + 1 board) to the next player.
    /// Returns true if a 4 was dealt face up (triggering a buy-card offer).
    /// After dealing, check HasPendingBuyCardOffers() to see if there's an offer to process.
    /// </summary>
    /// <returns>True if a 4 was dealt face up, false otherwise.</returns>
    public bool DealThirdStreetToNextPlayer()
    {
        if (CurrentPhase != Phases.ThirdStreet)
        {
            throw new InvalidOperationException("Cannot deal third street in current phase");
        }

        if (_nextPlayerToDeal >= _gamePlayers.Count)
        {
            throw new InvalidOperationException("All players have already been dealt to");
        }

        // Find the next active player
        while (_nextPlayerToDeal < _gamePlayers.Count && _gamePlayers[_nextPlayerToDeal].Player.HasFolded)
        {
            _nextPlayerToDeal++;
        }

        if (_nextPlayerToDeal >= _gamePlayers.Count)
        {
            return false;
        }

        var playerIndex = _nextPlayerToDeal;
        var gamePlayer = _gamePlayers[playerIndex];

        // Deal 2 hole cards (face down)
        gamePlayer.AddHoleCard(_dealer.DealCard());
        gamePlayer.AddHoleCard(_dealer.DealCard());
        // Deal 1 board card (face up)
        var boardCard = _dealer.DealCard();
        gamePlayer.AddBoardCard(boardCard);

        // Immediately queue buy-card offer if this player received a 4
        QueueBuyCardOffersForPlayer(playerIndex);

        _nextPlayerToDeal++;

        return boardCard.Symbol == Symbol.Four;
    }

    /// <summary>
    /// Finishes third street dealing by resetting bets and determining the bring-in player.
    /// Call this after all players have been dealt to and all buy-card offers have been processed.
    /// </summary>
    public void FinishThirdStreetDealing()
    {
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
    /// Queues buy-card offers for a specific player's pending 4s.
    /// </summary>
    private void QueueBuyCardOffersForPlayer(int playerIndex)
    {
        var gamePlayer = _gamePlayers[playerIndex];
        if (gamePlayer.Player.HasFolded)
        {
            return;
        }

        foreach (var boardCardIndex in gamePlayer.GetPendingFourOffers())
        {
            _pendingBuyCardOffers.Enqueue((playerIndex, boardCardIndex));
        }
    }

    /// <summary>
    /// Queues up buy-card offers for all pending 4s across all players.
    /// </summary>
    private void QueueBuyCardOffers()
    {
        _pendingBuyCardOffers.Clear();
        
        for (int i = 0; i < _gamePlayers.Count; i++)
        {
            QueueBuyCardOffersForPlayer(i);
        }
    }

    /// <summary>
    /// Checks if there are pending buy-card offers after dealing.
    /// </summary>
    public bool HasPendingBuyCardOffers()
    {
        return _pendingBuyCardOffers.Count > 0 || _currentBuyCardOffer.HasValue;
    }

    /// <summary>
    /// Gets the current buy-card offer details.
    /// </summary>
    public (BaseballGamePlayer player, Card fourCard)? GetCurrentBuyCardOffer()
    {
        if (!_currentBuyCardOffer.HasValue && _pendingBuyCardOffers.Count > 0)
        {
            _currentBuyCardOffer = _pendingBuyCardOffers.Dequeue();
        }

        if (!_currentBuyCardOffer.HasValue)
        {
            return null;
        }

        var (playerIndex, boardCardIndex) = _currentBuyCardOffer.Value;
        var gamePlayer = _gamePlayers[playerIndex];
        var fourCard = gamePlayer.BoardCards[boardCardIndex];
        
        return (gamePlayer, fourCard);
    }

    /// <summary>
    /// Processes a buy-card decision for the current offer.
    /// </summary>
    /// <param name="accept">True if the player wants to buy the extra card, false to decline.</param>
    /// <returns>Result of the buy-card action.</returns>
    public BuyCardResult ProcessBuyCardDecision(bool accept)
    {
        if (!_currentBuyCardOffer.HasValue)
        {
            return new BuyCardResult
            {
                Success = false,
                ErrorMessage = "No pending buy-card offer"
            };
        }

        var (playerIndex, boardCardIndex) = _currentBuyCardOffer.Value;
        var gamePlayer = _gamePlayers[playerIndex];

        if (accept)
        {
            // Check if player can afford the buy-card price
            if (gamePlayer.Player.ChipStack < _buyCardPrice)
            {
                return new BuyCardResult
                {
                    Success = false,
                    ErrorMessage = $"Insufficient chips. Need {_buyCardPrice}, have {gamePlayer.Player.ChipStack}"
                };
            }

            // Take the buy-card payment (not a bet, goes directly to pot)
            var actualAmount = gamePlayer.Player.PlaceBet(_buyCardPrice);
            _potManager.AddContribution(gamePlayer.Player.Name, actualAmount);
            
            // Reset the current bet since buy-card is NOT a bet
            gamePlayer.Player.ResetCurrentBet();

            // Deal an extra face-down card
            var extraCard = _dealer.DealCard();
            gamePlayer.AddHoleCard(extraCard);

            // Clear this offer
            gamePlayer.ClearPendingFourOffer(boardCardIndex);
            _currentBuyCardOffer = null;

            // Check if the extra card is also a 4 (which would trigger another offer)
            // Note: Extra cards are dealt face DOWN, so no buy-card option
            // (Per the rules: "Extra cards are always dealt face down")

            return new BuyCardResult
            {
                Success = true,
                PlayerName = gamePlayer.Player.Name,
                Purchased = true,
                AmountPaid = actualAmount,
                ExtraCard = extraCard
            };
        }
        else
        {
            // Player declined the offer
            gamePlayer.ClearPendingFourOffer(boardCardIndex);
            _currentBuyCardOffer = null;

            return new BuyCardResult
            {
                Success = true,
                PlayerName = gamePlayer.Player.Name,
                Purchased = false,
                AmountPaid = 0
            };
        }
    }

    /// <summary>
    /// Posts the bring-in bet for third street.
    /// Returns the bring-in action.
    /// Throws if bring-in is disabled or not in the correct phase.
    /// </summary>
    public BettingAction PostBringIn()
    {
        if (CurrentPhase != Phases.ThirdStreet)
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
            case Phases.ThirdStreet:
                if (_useBringIn && _bringInPlayerIndex >= 0)
                {
                    startPosition = _bringInPlayerIndex;
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
            case Phases.FourthStreet:
                startPosition = GetDealerPositionForFirstActingPlayer(FindBestVisibleHandPosition());
                minBet = _smallBet;
                break;
            case Phases.FifthStreet:
            case Phases.SixthStreet:
            case Phases.SeventhStreet:
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
    public BaseballGamePlayer GetBringInPlayer()
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
            CurrentPhase = Phases.Showdown;
            return;
        }

        // Reset current bets for next round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }

        switch (CurrentPhase)
        {
            case Phases.ThirdStreet:
                CurrentPhase = Phases.FourthStreet;
                break;
            case Phases.FourthStreet:
                CurrentPhase = Phases.FifthStreet;
                break;
            case Phases.FifthStreet:
                CurrentPhase = Phases.SixthStreet;
                break;
            case Phases.SixthStreet:
                CurrentPhase = Phases.SeventhStreet;
                break;
            case Phases.SeventhStreet:
                _potManager.CalculateSidePots(_gamePlayers.Select(gp => gp.Player));
                CurrentPhase = Phases.Showdown;
                break;
        }
    }

    /// <summary>
    /// Deals one card for the current street (4th-7th street) to all players at once.
    /// This method deals to all players before any buy-card offers are processed.
    /// For immediate buy-card processing (where each player's 4 offer is handled before 
    /// dealing to the next player), use StartDealingStreetCard() and DealStreetCardToNextPlayer() instead.
    /// </summary>
    public void DealStreetCard()
    {
        if (CurrentPhase is not (Phases.FourthStreet
            or Phases.FifthStreet
            or Phases.SixthStreet
            or Phases.SeventhStreet))
        {
            throw new InvalidOperationException("Cannot deal street card in current phase");
        }

        // Clear any existing offers and reset dealing state
        _pendingBuyCardOffers.Clear();
        _currentBuyCardOffer = null;
        _nextPlayerToDeal = 0;

        // Deal to all players
        while (HasMorePlayersToDeal())
        {
            DealStreetCardToNextPlayer();
        }

        FinishStreetCardDealing();
    }

    /// <summary>
    /// Starts the street card dealing process for incremental dealing.
    /// Use HasMorePlayersToDeal() and DealStreetCardToNextPlayer() to deal one player at a time.
    /// </summary>
    public void StartDealingStreetCard()
    {
        if (CurrentPhase is not (Phases.FourthStreet
            or Phases.FifthStreet
            or Phases.SixthStreet
            or Phases.SeventhStreet))
        {
            throw new InvalidOperationException("Cannot deal street card in current phase");
        }

        // Clear any existing offers and reset dealing state
        _pendingBuyCardOffers.Clear();
        _currentBuyCardOffer = null;
        _nextPlayerToDeal = 0;
    }

    /// <summary>
    /// Deals a street card to the next player.
    /// Returns true if a 4 was dealt face up (triggering a buy-card offer).
    /// On 7th street, cards are dealt face down and this always returns false.
    /// After dealing, check HasPendingBuyCardOffers() to see if there's an offer to process.
    /// </summary>
    /// <returns>True if a 4 was dealt face up, false otherwise.</returns>
    public bool DealStreetCardToNextPlayer()
    {
        if (CurrentPhase is not (Phases.FourthStreet
            or Phases.FifthStreet
            or Phases.SixthStreet
            or Phases.SeventhStreet))
        {
            throw new InvalidOperationException("Cannot deal street card in current phase");
        }

        if (_nextPlayerToDeal >= _gamePlayers.Count)
        {
            throw new InvalidOperationException("All players have already been dealt to");
        }

        // Find the next active player
        while (_nextPlayerToDeal < _gamePlayers.Count && _gamePlayers[_nextPlayerToDeal].Player.HasFolded)
        {
            _nextPlayerToDeal++;
        }

        if (_nextPlayerToDeal >= _gamePlayers.Count)
        {
            return false;
        }

        var playerIndex = _nextPlayerToDeal;
        var gamePlayer = _gamePlayers[playerIndex];
        Card card;

        if (CurrentPhase == Phases.SeventhStreet)
        {
            // Seventh street card is dealt face down - no buy-card option
            card = _dealer.DealCard();
            gamePlayer.AddHoleCard(card);
            _nextPlayerToDeal++;
            return false;
        }
        else
        {
            // 4th, 5th, 6th street cards are dealt face up
            card = _dealer.DealCard();
            gamePlayer.AddBoardCard(card);

            // Immediately queue buy-card offer if this player received a 4
            QueueBuyCardOffersForPlayer(playerIndex);

            _nextPlayerToDeal++;
            return card.Symbol == Symbol.Four;
        }
    }

    /// <summary>
    /// Finishes street card dealing by resetting bets.
    /// Call this after all players have been dealt to and all buy-card offers have been processed.
    /// </summary>
    public void FinishStreetCardDealing()
    {
        // Reset current bets before betting round
        foreach (var gamePlayer in _gamePlayers)
        {
            gamePlayer.Player.ResetCurrentBet();
        }
    }

    /// <summary>
    /// Performs the showdown and determines winners.
    /// </summary>
    public BaseballShowdownResult PerformShowdown()
    {
        if (CurrentPhase != Phases.Showdown)
        {
            return new BaseballShowdownResult
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

            CurrentPhase = Phases.Complete;
            MoveDealer();

            return new BaseballShowdownResult
            {
                Success = true,
                Payouts = new Dictionary<string, int> { { winner.Player.Name, totalPot } },
                PlayerHands = new Dictionary<string, (BaseballHand hand, IReadOnlyCollection<Card> cards)>
                {
                    { winner.Player.Name, (null, winner.AllCards.ToList()) }
                },
                WonByFold = true
            };
        }

        // Evaluate hands using BaseballHand
        var playerHands = playersInHand.ToDictionary(
            gp => gp.Player.Name,
            gp =>
            {
                var holeCards = gp.HoleCards.Take(2).ToList();
                var boardCards = gp.BoardCards.ToList();
                var downCards = gp.HoleCards.Skip(2).ToList();
                
                var hand = new BaseballHand(holeCards, boardCards, downCards);
                var allCards = gp.HoleCards.Concat(gp.BoardCards).ToList();
                    
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

        CurrentPhase = Phases.Complete;
        MoveDealer();

        return new BaseballShowdownResult
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
    /// </summary>
    private static int CompareCardsForBringIn(Card a, Card b)
    {
        if (a.Value != b.Value)
        {
            return a.Value.CompareTo(b.Value);
        }

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
    /// Finds the position of the player with the best visible hand.
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
        
        var maxCount = valueCounts.First().Count();
        
        if (maxCount >= 4)
        {
            strength = 7_000_000 + valueCounts.First().Key * 1000;
        }
        else if (maxCount >= 3)
        {
            strength = 4_000_000 + valueCounts.First().Key * 1000;
        }
        else if (maxCount >= 2)
        {
            var pairs = valueCounts.Where(g => g.Count() >= 2).ToList();
            if (pairs.Count >= 2)
            {
                strength = 3_000_000 + pairs[0].Key * 1000 + pairs[1].Key * 10;
            }
            else
            {
                strength = 2_000_000 + pairs[0].Key * 1000;
            }
        }
        else
        {
            strength = 1_000_000;
        }

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
            Phases.ThirdStreet => _smallBet,
            Phases.FourthStreet => _smallBet,
            Phases.FifthStreet => _bigBet,
            Phases.SixthStreet => _bigBet,
            Phases.SeventhStreet => _bigBet,
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
            Phases.ThirdStreet => "Third Street",
            Phases.FourthStreet => "Fourth Street",
            Phases.FifthStreet => "Fifth Street",
            Phases.SixthStreet => "Sixth Street",
            Phases.SeventhStreet => "Seventh Street (River)",
            _ => CurrentPhase.ToString()
        };
    }


}