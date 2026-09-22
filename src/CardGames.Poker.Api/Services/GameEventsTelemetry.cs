using System.Diagnostics.Metrics;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Services;

public sealed class GameEventsTelemetry
{
	public const string MeterName = "CardGames.Poker.Api.Games";

	private readonly Counter<long> _gamesCreated;
	private readonly Counter<long> _gamesStarted;
	private readonly Counter<long> _gamesEnded;
	private readonly Counter<long> _gamesCancelled;
	private readonly Counter<long> _handsDealt;
	private readonly Counter<long> _handsCompleted;
	private readonly Histogram<double> _handDurationMs;
	private readonly Counter<long> _playerEvents;

	public GameEventsTelemetry(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create(MeterName);
		_gamesCreated = meter.CreateCounter<long>("games_created_total");
		_gamesStarted = meter.CreateCounter<long>("games_started_total");
		_gamesEnded = meter.CreateCounter<long>("games_ended_total");
		_gamesCancelled = meter.CreateCounter<long>("games_cancelled_total");
		_handsDealt = meter.CreateCounter<long>("hands_dealt_total");
		_handsCompleted = meter.CreateCounter<long>("hands_completed_total");
		_handDurationMs = meter.CreateHistogram<double>("hand_duration_ms", unit: "ms");
		_playerEvents = meter.CreateCounter<long>("player_events_total");
	}

	/// <summary>
	/// Records a created game.
	/// </summary>
	public void RecordGameCreated(string gameType, string tableKind)
	{
		_gamesCreated.Add(1,
			new("game_type", Normalize(gameType, "unknown")),
			new("table_kind", Normalize(tableKind, "standard")));
	}

	/// <summary>
	/// Records the first start of a game.
	/// </summary>
	public void RecordGameStarted(string gameType, string tableKind)
	{
		_gamesStarted.Add(1,
			new("game_type", Normalize(gameType, "unknown")),
			new("table_kind", Normalize(tableKind, "standard")));
	}

	/// <summary>
	/// Records a game end event.
	/// </summary>
	public void RecordGameEnded(string gameType, string tableKind, string reason)
	{
		_gamesEnded.Add(1,
			new("game_type", Normalize(gameType, "unknown")),
			new("table_kind", Normalize(tableKind, "standard")),
			new("reason", Normalize(reason, "completed")));
	}

	/// <summary>
	/// Records a cancelled game.
	/// </summary>
	public void RecordGameCancelled(string gameType, string tableKind)
	{
		_gamesCancelled.Add(1,
			new("game_type", Normalize(gameType, "unknown")),
			new("table_kind", Normalize(tableKind, "standard")));
	}

	/// <summary>
	/// Records a dealt hand.
	/// </summary>
	public void RecordHandDealt(string gameType, string tableKind)
	{
		_handsDealt.Add(1,
			new("game_type", Normalize(gameType, "unknown")),
			new("table_kind", Normalize(tableKind, "standard")));
	}

	/// <summary>
	/// Records a completed hand.
	/// </summary>
	public void RecordHandCompleted(string gameType, string tableKind)
	{
		_handsCompleted.Add(1,
			new("game_type", Normalize(gameType, "unknown")),
			new("table_kind", Normalize(tableKind, "standard")));
	}

	/// <summary>
	/// Records the duration of a hand.
	/// </summary>
	public void RecordHandDuration(string gameType, string tableKind, double durationMs)
	{
		_handDurationMs.Record(durationMs,
			new("game_type", Normalize(gameType, "unknown")),
			new("table_kind", Normalize(tableKind, "standard")));
	}

	/// <summary>
	/// Records a player-level event.
	/// </summary>
	public void RecordPlayerEvent(string gameType, string tableKind, string eventName)
	{
		_playerEvents.Add(1,
			new("game_type", Normalize(gameType, "unknown")),
			new("table_kind", Normalize(tableKind, "standard")),
			new("event", Normalize(eventName, "unknown")));
	}

	internal static string ResolveGameType(Game game)
	{
		ArgumentNullException.ThrowIfNull(game);
		return Normalize(game.CurrentHandGameTypeCode ?? game.GameType?.Code, game.IsDealersChoice ? "dealers_choice" : "unknown");
	}

	internal static string ResolveTableKind(Game game)
	{
		ArgumentNullException.ThrowIfNull(game);
		return game.IsDealersChoice ? "dealers_choice" : "standard";
	}

	internal static double? TryGetHandDurationMs(IEnumerable<Pot> currentHandPots, DateTimeOffset completedAt)
	{
		ArgumentNullException.ThrowIfNull(currentHandPots);

		var handStartedAt = currentHandPots
			.Select(p => (DateTimeOffset?)p.CreatedAt)
			.OrderBy(createdAt => createdAt)
			.FirstOrDefault();

		if (!handStartedAt.HasValue)
		{
			return null;
		}

		return Math.Max(0, (completedAt - handStartedAt.Value).TotalMilliseconds);
	}

	private static string Normalize(string? value, string fallback)
	{
		return string.IsNullOrWhiteSpace(value)
			? fallback
			: value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('\'', '_');
	}
}
