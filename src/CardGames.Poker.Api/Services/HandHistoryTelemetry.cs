using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Services;

public sealed class HandHistoryTelemetry
{
	public const string MeterName = "CardGames.Poker.Api.HandHistory";

	private readonly Counter<long> _recorded;

	public HandHistoryTelemetry(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create(MeterName);
		// game_type: variant code (low cardinality) ; outcome: recorded | failed
		_recorded = meter.CreateCounter<long>("hand_history_recorded_total");
	}

	public void RecordOutcome(string gameType, string outcome)
		=> _recorded.Add(1, new("game_type", gameType), new("outcome", outcome));
}
