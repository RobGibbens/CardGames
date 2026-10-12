using System.Collections.Concurrent;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CardGames.Poker.Api.Services;

/// <summary>
///     Manages action timers for player turns and broadcasts timer updates via SignalR.
/// </summary>
public sealed class ActionTimerService : IActionTimerService, IDisposable
{
	private const string GameGroupPrefix = "game:";

	private readonly ConcurrentDictionary<Guid, GameTimerState> _activeTimers = new();
	private readonly IHubContext<GameHub> _hubContext;
	private readonly ILogger<ActionTimerService> _logger;
	private readonly IServiceScopeFactory _scopeFactory;
	private bool _disposed;

	/// <summary>
	///     Initializes a new instance of the <see cref="ActionTimerService" /> class.
	/// </summary>
	public ActionTimerService(
		IHubContext<GameHub> hubContext,
		IServiceScopeFactory scopeFactory,
		ILogger<ActionTimerService> logger)
	{
		_hubContext = hubContext;
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	/// <inheritdoc />
	public void StartTimer(Guid gameId, int playerSeatIndex,
		int durationSeconds = IActionTimerService.DefaultTimerDurationSeconds, Func<Guid, int, Task>? onExpired = null,
		TimeSpan? startDelay = null, int handNumber = 0, ulong stateVersion = 0)
	{
		// Stop any existing timer for this game
		StopTimer(gameId);

		var effectiveStartDelay = startDelay.GetValueOrDefault();
		if (effectiveStartDelay < TimeSpan.Zero) effectiveStartDelay = TimeSpan.Zero;

		var state = new ActionTimerState
		{
			GameId = gameId,
			PlayerSeatIndex = playerSeatIndex,
			DurationSeconds = durationSeconds,
			StartedAtUtc = DateTimeOffset.UtcNow.Add(effectiveStartDelay),
			HandNumber = handNumber,
			StateVersion = stateVersion
		};

		var timerState = new GameTimerState
		{
			State = state,
			OnExpired = onExpired
		};

		// Create the timer that ticks every second
		var timer = new Timer(
			async _ => await OnTimerTickAsync(gameId),
			null,
			TimeSpan.FromSeconds(1),
			TimeSpan.FromSeconds(1));

		timerState.Timer = timer;

		_activeTimers[gameId] = timerState;

		_logger.LogInformation(
			"Started action timer for game {GameId}, player seat {SeatIndex}, duration {Duration}s, start delay {StartDelayMs}ms",
			gameId, playerSeatIndex, durationSeconds, effectiveStartDelay.TotalMilliseconds);

		// Broadcast initial timer state
		_ = BroadcastTimerStateAsync(gameId, state);
	}

	/// <inheritdoc />
	public void StartChipCheckPauseTimer(
		Guid gameId,
		int durationSeconds = IActionTimerService.DefaultChipCheckPauseDurationSeconds,
		Func<Guid, Task>? onExpired = null,
		DateTimeOffset? startedAtUtc = null)
	{
		// Stop any existing timer for this game
		StopTimer(gameId);

		var state = new ActionTimerState
		{
			GameId = gameId,
			PlayerSeatIndex = -1, // No specific player for chip check pause
			DurationSeconds = durationSeconds,
			StartedAtUtc = startedAtUtc ?? DateTimeOffset.UtcNow,
			TimerType = ActionTimerType.ChipCheckPause,
			HandNumber = 0,
			StateVersion = 0
		};

		var timerState = new GameTimerState
		{
			State = state,
			OnChipCheckExpired = onExpired
		};

		// Create the timer that ticks every second
		var timer = new Timer(
			async _ => await OnTimerTickAsync(gameId),
			null,
			TimeSpan.FromSeconds(1),
			TimeSpan.FromSeconds(1));

		timerState.Timer = timer;

		_activeTimers[gameId] = timerState;

		_logger.LogInformation(
			"Started chip check pause timer for game {GameId}, duration {Duration}s",
			gameId, durationSeconds);

		// Broadcast initial timer state
		_ = BroadcastTimerStateAsync(gameId, state);
	}

	/// <inheritdoc />
	public void StopTimer(Guid gameId)
	{
		if (_activeTimers.TryRemove(gameId, out var timerState))
		{
			timerState.Timer?.Dispose();
			_logger.LogInformation("Stopped action timer for game {GameId}", gameId);

			// Broadcast the timer that just stopped, keeping its seat and start time.
			_ = BroadcastTimerStoppedAsync(timerState.State);
		}
	}

	/// <inheritdoc />
	public ActionTimerState? GetTimerState(Guid gameId)
	{
		return _activeTimers.TryGetValue(gameId, out var timerState) ? timerState.State : null;
	}

	/// <inheritdoc />
	public bool IsTimerActive(Guid gameId)
	{
		return _activeTimers.ContainsKey(gameId);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed) return;

		_disposed = true;

		foreach (var kvp in _activeTimers) kvp.Value.Timer?.Dispose();

		_activeTimers.Clear();
	}

	private async Task OnTimerTickAsync(Guid gameId)
	{
		if (_disposed) return;

		if (!_activeTimers.TryGetValue(gameId, out var timerState)) return;

		var state = timerState.State;
		var secondsRemaining = state.SecondsRemaining;

		// The <= 0 branch broadcasts the inactive stop for this timer and does not call BroadcastTimerStateAsync.
		if (secondsRemaining <= 0)
		{
			_logger.LogInformation(
				"Action timer expired for game {GameId}, player seat {SeatIndex}, type {TimerType}",
				gameId, state.PlayerSeatIndex, state.TimerType);

			// Stop the timer. StopTimer broadcasts ToStoppedDto for the removed state.
			StopTimer(gameId);

			// Invoke appropriate expiration callback based on timer type
			if (state.TimerType == ActionTimerType.ChipCheckPause && timerState.OnChipCheckExpired is not null)
				try
				{
					await timerState.OnChipCheckExpired(gameId);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in chip check timer expiration callback for game {GameId}", gameId);
				}
			else if (timerState.OnExpired is not null)
				try
				{
					await timerState.OnExpired(gameId, state.PlayerSeatIndex);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error in timer expiration callback for game {GameId}", gameId);
				}

			return;
		}

		await BroadcastTimerStateAsync(gameId, state);
	}

	private async Task BroadcastTimerStateAsync(Guid gameId, ActionTimerState state)
	{
		try
		{
			var groupName = GetGroupName(gameId);
			await _hubContext.Clients.Group(groupName)
				.SendAsync("ActionTimerUpdated", ToActiveDto(state));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error broadcasting timer state for game {GameId}", gameId);
		}
	}

	private async Task BroadcastTimerStoppedAsync(ActionTimerState state)
	{
		try
		{
			var groupName = GetGroupName(state.GameId);
			await _hubContext.Clients.Group(groupName)
				.SendAsync("ActionTimerUpdated", ToStoppedDto(state));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error broadcasting timer stopped for game {GameId}", state.GameId);
		}
	}

	internal static ActionTimerStateDto ToActiveDto(ActionTimerState state)
	{
		return new ActionTimerStateDto
		{
			SecondsRemaining = state.SecondsRemaining,
			DurationSeconds = state.DurationSeconds,
			StartedAtUtc = state.StartedAtUtc,
			PlayerSeatIndex = state.PlayerSeatIndex,
			IsActive = true,
			HandNumber = state.HandNumber,
			StateVersion = state.StateVersion
		};
	}

	internal static ActionTimerStateDto ToStoppedDto(ActionTimerState state)
	{
		return new ActionTimerStateDto
		{
			SecondsRemaining = 0,
			DurationSeconds = state.DurationSeconds,
			StartedAtUtc = state.StartedAtUtc,
			PlayerSeatIndex = state.PlayerSeatIndex,
			IsActive = false,
			HandNumber = state.HandNumber,
			StateVersion = state.StateVersion
		};
	}

	private static string GetGroupName(Guid gameId)
	{
		return $"{GameGroupPrefix}{gameId}";
	}

	private sealed class GameTimerState
	{
		public required ActionTimerState State { get; init; }
		public Timer? Timer { get; set; }
		public Func<Guid, int, Task>? OnExpired { get; init; }
		public Func<Guid, Task>? OnChipCheckExpired { get; init; }
	}
}