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
}