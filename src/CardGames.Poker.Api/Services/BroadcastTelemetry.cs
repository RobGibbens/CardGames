using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Services;

public sealed class BroadcastTelemetry
{
	public const string MeterName = "CardGames.Poker.Api.Realtime";

	private readonly Counter<long> _broadcasts;
	private readonly Histogram<double> _broadcastDurationMs;

	public BroadcastTelemetry(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create(MeterName);

		_broadcasts = meter.CreateCounter<long>("realtime_broadcasts_total");
		_broadcastDurationMs = meter.CreateHistogram<double>("realtime_broadcast_duration_ms", unit: "ms");
	}

	public void RecordBroadcast(string hub, string @event, string outcome, double durationMs)
	{
		_broadcasts.Add(1, new("hub", hub), new("event", @event), new("outcome", outcome));
		_broadcastDurationMs.Record(durationMs, new("hub", hub), new("event", @event));
	}
}
