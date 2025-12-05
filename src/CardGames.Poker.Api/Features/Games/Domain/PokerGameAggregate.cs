using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Features.Games.Domain.Enums;
using CardGames.Poker.Api.Features.Games.Domain.Events;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;

namespace CardGames.Poker.Api.Features.Games.Domain;

/// <summary>
/// Aggregate root for a poker game, managing state through event sourcing.
/// Wraps existing game domain models (e.g., FiveCardDrawGame) while providing
/// event-driven state management compatible with Marten.
/// </summary>
public class PokerGameAggregate
{
	// Identity
	public Guid Id { get; private set; }

	// State
	public GameType GameType { get; private set; }
	public GameStatus Status { get; private set; }
	public GameConfiguration Configuration { get; private set; }
	public DateTime CreatedAt { get; private set; }

	// Players
	public List<GamePlayer> Players { get; private set; } = [];

	public Guid? CurrentHandId { get; private set; }
	public int HandNumber { get; private set; }
	public HandPhase CurrentPhase { get; private set; }
	public int DealerPosition { get; private set; }
	public int TotalPot { get; private set; }
	public Guid? CurrentPlayerToAct { get; private set; }
	public int CurrentBet { get; private set; }

	private FiveCardDrawGame? _gameInstance;
	private Dictionary<Guid, string> _playerIdToName = new();
	private Dictionary<string, Guid> _playerNameToId = new();

	// Marten requires a default constructor
	public PokerGameAggregate() { }

	/// <summary>
	/// Apply method for GameCreated event (Marten convention).
	/// </summary>
	public void Apply(GameCreated @event)
	{
		Id = @event.GameId;
		GameType = @event.GameType;
		Configuration = @event.Configuration;
		Status = GameStatus.WaitingForPlayers;
		CreatedAt = @event.CreatedAt;
	}

	/// <summary>
	/// Apply method for PlayerJoined event (Marten convention).
	/// </summary>
	public void Apply(PlayerJoined @event)
	{
		Players.Add(new GamePlayer(
			@event.PlayerId,
			@event.PlayerName,
			@event.BuyIn,
			@event.Position
		));

		// Update status if we have minimum players
		if (Players.Count >= 2)
		{
			Status = GameStatus.ReadyToStart;
		}
	}

	public void Apply(HandStarted @event)
	{
		CurrentHandId = @event.HandId;
		HandNumber = @event.HandNumber;
		DealerPosition = @event.DealerPosition;
		CurrentPhase = HandPhase.CollectingAntes;
		Status = GameStatus.InProgress;
		TotalPot = 0;
		CurrentBet = 0;

		// Initialize internal game instance
		InitializeGameInstance();
	}

	public void Apply(AntesCollected @event)
	{
		TotalPot = @event.TotalCollected;
		CurrentPhase = HandPhase.Dealing;

		// Update player chip stacks
		foreach (var (playerId, amount) in @event.PlayerAntes)
		{
			var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
			if (player != null)
			{
				player.DeductChips(amount);
				player.CurrentBet = amount;
			}
		}
	}

	public void Apply(CardsDealt @event)
	{
		CurrentPhase = HandPhase.FirstBettingRound;
		// Reset current bets for betting round
		foreach (var player in Players)
		{
			player.CurrentBet = 0;
		}
		CurrentBet = 0;

		// Set first player to act
		CurrentPlayerToAct = GetFirstPlayerToAct();
	}

	public void Apply(CardsDealtInternal @event)
	{
		// Store the actual cards for each player
		foreach (var (playerId, cards) in @event.PlayerCards)
		{
			var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
			if (player != null)
			{
				player.Cards = cards;
			}
		}
	}

	public void Apply(BettingActionPerformed @event)
	{
		TotalPot = @event.NewPot;

		var player = Players.FirstOrDefault(p => p.PlayerId == @event.PlayerId);
		if (player != null)
		{
			player.ChipStack = @event.PlayerChipStack;

			if (@event.ActionType == BettingActionType.Fold)
			{
				player.HasFolded = true;
			}
			else if (@event.ActionType == BettingActionType.AllIn)
			{
				player.IsAllIn = true;
			}
		}

		if (@event.RoundComplete && @event.NewPhase != null)
		{
			CurrentPhase = Enum.Parse<HandPhase>(@event.NewPhase);
		}

		// Update current bet and next player
		UpdateCurrentBetAndNextPlayer();
	}

	public bool CanStartHand()
	{
		return (Status == GameStatus.ReadyToStart || Status == GameStatus.InProgress)
			&& CurrentPhase == HandPhase.None
			&& Players.Count(p => p.ChipStack > 0) >= 2;
	}

	/// <summary>
	/// Collects antes from all players.
	/// </summary>
	public CollectAntesResult CollectAntes()
	{
		if (CurrentPhase != HandPhase.CollectingAntes)
		{
			return new CollectAntesResult { Success = false, ErrorMessage = "Cannot collect antes in current phase." };
		}

		if (_gameInstance == null)
		{
			return new CollectAntesResult { Success = false, ErrorMessage = "No active hand." };
		}

		_gameInstance.StartHand();
		var actions = _gameInstance.CollectAntes();

		var playerAntes = new Dictionary<Guid, int>();
		var totalCollected = 0;

		foreach (var action in actions)
		{
			if (_playerNameToId.TryGetValue(action.PlayerName, out var playerId))
			{
				playerAntes[playerId] = action.Amount;
				totalCollected += action.Amount;
			}
		}

		CurrentPhase = HandPhase.Dealing;
		TotalPot = totalCollected;

		return new CollectAntesResult
		{
			Success = true,
			PlayerAntes = playerAntes,
			TotalCollected = totalCollected
		};
	}

	/// <summary>
	/// Deals cards to all active players.
	/// </summary>
	public DealCardsResult DealCards()
	{
		if (CurrentPhase != HandPhase.Dealing)
		{
			return new DealCardsResult { Success = false, ErrorMessage = "Cannot deal in current phase." };
		}

		if (_gameInstance == null)
		{
			return new DealCardsResult { Success = false, ErrorMessage = "No active hand." };
		}

		_gameInstance.DealHands();

		var playerCardCounts = new Dictionary<Guid, int>();
		var playerCards = new Dictionary<Guid, List<string>>();

		foreach (var gamePlayer in _gameInstance.GamePlayers)
		{
			if (_playerNameToId.TryGetValue(gamePlayer.Player.Name, out var playerId))
			{
				var cards = gamePlayer.Hand.Select(c => c.ToShortString()).ToList();
				playerCards[playerId] = cards;
				playerCardCounts[playerId] = cards.Count;

				// Update the aggregate's player cards
				var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
				if (player != null)
				{
					player.Cards = cards;
				}
			}
		}

		CurrentPhase = HandPhase.FirstBettingRound;
		CurrentPlayerToAct = GetFirstPlayerToAct();

		return new DealCardsResult
		{
			Success = true,
			PlayerCardCounts = playerCardCounts,
			PlayerCards = playerCards
		};
	}

	public int GetNextDealerPosition()
	{
		return (DealerPosition + 1) % Players.Count;
	}

	public bool IsPlayerTurn(Guid playerId)
	{
		return CurrentPlayerToAct == playerId;
	}

	public AvailableActions GetAvailableActions()
	{
		if (_gameInstance == null || CurrentPlayerToAct == null)
		{
			return new AvailableActions();
		}
		return _gameInstance.GetAvailableActions();
	}

	public BettingResult ProcessBettingAction(Guid playerId, BettingActionType actionType, int amount)
	{
		if (_gameInstance == null)
		{
			return new BettingResult { Success = false, ErrorMessage = "No active hand" };
		}

		var result = _gameInstance.ProcessBettingAction(actionType, amount);

		return new BettingResult
		{
			Success = result.Success,
			ErrorMessage = result.ErrorMessage,
			ActionDescription = result.Action?.ToString() ?? "",
			ActualAmount = result.Action?.Amount ?? 0,
			RoundComplete = result.RoundComplete,
			PhaseAdvanced = result.RoundComplete,
			NewPhase = result.RoundComplete ? GetNextPhase().ToString() : null,
			PlayerChipStack = GetPlayerChipStack(playerId)
		};
	}

	private void InitializeGameInstance()
	{
		var playerTuples = Players.Select(p => (p.Name, p.ChipStack)).ToList();
		_gameInstance = new FiveCardDrawGame(playerTuples, Configuration.Ante, Configuration.MinBet);

		_playerIdToName = Players.ToDictionary(p => p.PlayerId, p => p.Name);
		_playerNameToId = Players.ToDictionary(p => p.Name, p => p.PlayerId);
	}

	private Guid? GetFirstPlayerToAct()
	{
		var activePlayer = Players
			.Skip((DealerPosition + 1) % Players.Count)
			.Concat(Players.Take((DealerPosition + 1) % Players.Count))
			.FirstOrDefault(p => !p.HasFolded && !p.IsAllIn && p.ChipStack > 0);

		return activePlayer?.PlayerId;
	}

	private void UpdateCurrentBetAndNextPlayer()
	{
		// Logic to determine next player based on game state
		if (_gameInstance != null)
		{
			var currentPlayer = _gameInstance.GetCurrentPlayer();
			if (currentPlayer != null && _playerNameToId.TryGetValue(currentPlayer.Name, out var playerId))
			{
				CurrentPlayerToAct = playerId;
			}
			else
			{
				CurrentPlayerToAct = null;
			}
			CurrentBet = _gameInstance.CurrentBettingRound?.CurrentBet ?? 0;
		}
	}

	private HandPhase GetNextPhase()
	{
		return CurrentPhase switch
		{
			HandPhase.FirstBettingRound => HandPhase.DrawPhase,
			HandPhase.DrawPhase => HandPhase.SecondBettingRound,
			HandPhase.SecondBettingRound => HandPhase.Showdown,
			_ => HandPhase.Complete
		};
	}

	private int GetPlayerChipStack(Guid playerId)
	{
		return Players.FirstOrDefault(p => p.PlayerId == playerId)?.ChipStack ?? 0;
	}

	/// <summary>
	/// Check if a player can join the game.
	/// </summary>
	public bool CanPlayerJoin()
	{
		return Status == GameStatus.WaitingForPlayers || Status == GameStatus.ReadyToStart;
	}

	/// <summary>
	/// Check if the game is at maximum capacity.
	/// </summary>
	public bool IsFull()
	{
		return Players.Count >= Configuration.MaxPlayers;
	}

	/// <summary>
	/// Get the next available seat position.
	/// </summary>
	public int GetNextPosition()
	{
		return Players.Count;
	}

	/// <summary>
	/// Apply method for DrawCardsPerformed event (Marten convention).
	/// </summary>
	public void Apply(DrawCardsPerformed @event)
	{
		// Update the player's cards
		var player = Players.FirstOrDefault(p => p.PlayerId == @event.PlayerId);
		if (player != null)
		{
			player.Cards = @event.NewCards;
		}

		// Update phase and next player
		if (@event.DrawPhaseComplete)
		{
			CurrentPhase = HandPhase.SecondBettingRound;
		}
		CurrentPlayerToAct = @event.NextPlayerToAct;
	}

	/// <summary>
	/// Apply method for ShowdownPerformed event (Marten convention).
	/// </summary>
	public void Apply(ShowdownPerformed @event)
	{
		// Update player chip stacks with payouts
		foreach (var result in @event.Results)
		{
			var player = Players.FirstOrDefault(p => p.PlayerId == result.PlayerId);
			if (player != null)
			{
				player.ChipStack = result.FinalChipStack;
			}
		}

		CurrentPhase = HandPhase.Complete;
		TotalPot = 0;
		CurrentPlayerToAct = null;
	}

	/// <summary>
	/// Gets the current player who needs to draw cards.
	/// </summary>
	public (Guid? PlayerId, string? PlayerName) GetCurrentDrawPlayer()
	{
		if (_gameInstance == null || CurrentPhase != HandPhase.DrawPhase)
		{
			return (null, null);
		}

		var drawPlayer = _gameInstance.GetCurrentDrawPlayer();
		if (drawPlayer == null)
		{
			return (null, null);
		}

		if (_playerNameToId.TryGetValue(drawPlayer.Player.Name, out var playerId))
		{
			return (playerId, drawPlayer.Player.Name);
		}

		return (null, null);
	}

	/// <summary>
	/// Processes a draw action (discard and draw cards) for a player.
	/// </summary>
	public DrawCardsApiResult ProcessDraw(Guid playerId, List<int> discardIndices)
	{
		if (_gameInstance == null)
		{
			return new DrawCardsApiResult { Success = false, ErrorMessage = "No active hand" };
		}

		if (CurrentPhase != HandPhase.DrawPhase)
		{
			return new DrawCardsApiResult { Success = false, ErrorMessage = "Not in draw phase" };
		}

		var currentDrawPlayer = _gameInstance.GetCurrentDrawPlayer();
		if (currentDrawPlayer == null)
		{
			return new DrawCardsApiResult { Success = false, ErrorMessage = "No player to draw" };
		}

		if (!_playerNameToId.TryGetValue(currentDrawPlayer.Player.Name, out var currentPlayerId) || currentPlayerId != playerId)
		{
			return new DrawCardsApiResult { Success = false, ErrorMessage = "It is not this player's turn to draw" };
		}

		var result = _gameInstance.ProcessDraw(discardIndices);

		if (!result.Success)
		{
			return new DrawCardsApiResult { Success = false, ErrorMessage = result.ErrorMessage };
		}

		// Get the new hand for this player
		var newCards = result.NewCards?.Select(c => c.ToShortString()).ToList() ?? [];
		var newHand = currentDrawPlayer.Hand.Select(c => c.ToShortString()).ToList();

		// Update aggregate's player cards
		var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
		if (player != null)
		{
			player.Cards = newHand;
		}

		// Check if draw phase is complete
		var drawPhaseComplete = result.DrawComplete;
		Guid? nextPlayerToAct = null;

		if (drawPhaseComplete)
		{
			// Move to second betting round
			CurrentPhase = HandPhase.SecondBettingRound;
			var nextBettingPlayer = _gameInstance.GetCurrentPlayer();
			if (nextBettingPlayer != null && _playerNameToId.TryGetValue(nextBettingPlayer.Name, out var nextId))
			{
				nextPlayerToAct = nextId;
			}
		}
		else
		{
			// Get next draw player
			var nextDrawPlayer = _gameInstance.GetCurrentDrawPlayer();
			if (nextDrawPlayer != null && _playerNameToId.TryGetValue(nextDrawPlayer.Player.Name, out var nextId))
			{
				nextPlayerToAct = nextId;
			}
		}

		CurrentPlayerToAct = nextPlayerToAct;

		return new DrawCardsApiResult
		{
			Success = true,
			PlayerId = playerId,
			PlayerName = result.PlayerName ?? "",
			CardsDiscarded = discardIndices.Count,
			NewCards = newCards,
			NewHand = newHand,
			DrawPhaseComplete = drawPhaseComplete,
			NextPlayerToAct = nextPlayerToAct
		};
	}

	/// <summary>
	/// Checks if showdown can be performed.
	/// </summary>
	public bool CanPerformShowdown()
	{
		// Showdown can happen if:
		// 1. We're in showdown phase, OR
		// 2. Only one player remains (everyone else folded)
		if (CurrentPhase == HandPhase.Showdown)
		{
			return true;
		}

		var activePlayersCount = Players.Count(p => !p.HasFolded);
		return activePlayersCount <= 1 && CurrentHandId != null;
	}

	/// <summary>
	/// Performs the showdown and determines winners.
	/// </summary>
	public ShowdownApiResult PerformShowdown()
	{
		if (_gameInstance == null)
		{
			return new ShowdownApiResult { Success = false, ErrorMessage = "No active hand" };
		}

		// Force showdown phase if not already there
		if (_gameInstance.CurrentPhase != FiveCardDrawPhase.Showdown)
		{
			// Check if we should be in showdown (e.g., only one player left)
			var activePlayersCount = Players.Count(p => !p.HasFolded);
			if (activePlayersCount > 1 && CurrentPhase != HandPhase.Showdown)
			{
				return new ShowdownApiResult { Success = false, ErrorMessage = "Cannot perform showdown in current phase" };
			}
		}

		var result = _gameInstance.PerformShowdown();

		if (!result.Success)
		{
			return new ShowdownApiResult { Success = false, ErrorMessage = result.ErrorMessage };
		}

		var showdownResults = new List<ShowdownPlayerResult>();

		foreach (var (playerName, (hand, cards)) in result.PlayerHands ?? [])
		{
			if (_playerNameToId.TryGetValue(playerName, out var playerId))
			{
				var payout = result.Payouts?.GetValueOrDefault(playerName, 0) ?? 0;
				var isWinner = payout > 0;
				var player = Players.FirstOrDefault(p => p.PlayerId == playerId);

				// Update player chips
				if (player != null && payout > 0)
				{
					player.AddChips(payout);
				}

				var handDescription = hand != null
					? GetHandDescription(hand.Type)
					: (result.WonByFold ? "Win by fold" : "Unknown");

				showdownResults.Add(new ShowdownPlayerResult
				{
					PlayerId = playerId,
					PlayerName = playerName,
					Hand = cards.Select(c => c.ToShortString()).ToList(),
					HandType = hand?.Type.ToString() ?? "Unknown",
					HandDescription = handDescription,
					Payout = payout,
					IsWinner = isWinner
				});
			}
		}

		// If won by fold, add the winner
		if (result.WonByFold && showdownResults.Count == 1)
		{
			var winner = showdownResults[0];
			winner = winner with { HandDescription = "Win by fold - all other players folded" };
			showdownResults = [winner];
		}

		CurrentPhase = HandPhase.Complete;
		TotalPot = 0;

		return new ShowdownApiResult
		{
			Success = true,
			WonByFold = result.WonByFold,
			Results = showdownResults
		};
	}

	/// <summary>
	/// Resets the hand state for a new hand.
	/// </summary>
	public void ResetForNewHand()
	{
		CurrentHandId = null;
		CurrentPhase = HandPhase.None;
		TotalPot = 0;
		CurrentBet = 0;
		CurrentPlayerToAct = null;
		_gameInstance = null;

		foreach (var player in Players)
		{
			player.ResetForNewHand();
		}
	}

	/// <summary>
	/// Checks if the game can continue (at least 2 players have chips).
	/// </summary>
	public bool CanContinueGame()
	{
		return Players.Count(p => p.ChipStack > 0) >= 2;
	}

	/// <summary>
	/// Gets the players who can continue playing (have chips).
	/// </summary>
	public List<GamePlayer> GetPlayersWithChips()
	{
		return Players.Where(p => p.ChipStack > 0).ToList();
	}

	/// <summary>
	/// Gets a human-readable description for a hand type.
	/// </summary>
	private static string GetHandDescription(Poker.Hands.HandTypes.HandType handType)
	{
		return handType switch
		{
			Poker.Hands.HandTypes.HandType.HighCard => "High Card",
			Poker.Hands.HandTypes.HandType.OnePair => "One Pair",
			Poker.Hands.HandTypes.HandType.TwoPair => "Two Pair",
			Poker.Hands.HandTypes.HandType.Trips => "Three of a Kind",
			Poker.Hands.HandTypes.HandType.Straight => "Straight",
			Poker.Hands.HandTypes.HandType.Flush => "Flush",
			Poker.Hands.HandTypes.HandType.FullHouse => "Full House",
			Poker.Hands.HandTypes.HandType.Quads => "Four of a Kind",
			Poker.Hands.HandTypes.HandType.StraightFlush => "Straight Flush",
			Poker.Hands.HandTypes.HandType.FiveOfAKind => "Five of a Kind",
			_ => "Unknown"
		};
	}
}