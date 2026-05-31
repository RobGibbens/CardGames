using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Services;

public sealed class ContinuousPlayTelemetry
{
	public const string MeterName = "CardGames.Poker.Api.ContinuousPlay";

	private readonly Counter<long> _gamesProcessed;
	private readonly Histogram<double> _iterationDurationMs;

	public ContinuousPlayTelemetry(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create(MeterName);

		_gamesProcessed = meter.CreateCounter<long>("continuous_play_games_processed_total");
		_iterationDurationMs = meter.CreateHistogram<double>("continuous_play_iteration_duration_ms", unit: "ms");
	}

	public void RecordGameProcessed(string phase, string outcome)
		=> _gamesProcessed.Add(1, new("phase", phase), new("outcome", outcome));

	public void RecordIteration(double durationMs)
		=> _iterationDurationMs.Record(durationMs);
}
