using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Features.Leagues.v1.Telemetry;

public sealed class LeaguesTelemetry
{
	private readonly Histogram<double> _endpointLatencyMs;
	private readonly Counter<long> _funnelAttempts;

	public LeaguesTelemetry(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create("CardGames.Poker.Api.Leagues");
		_funnelAttempts = meter.CreateCounter<long>("leagues_funnel_attempts_total");
		_endpointLatencyMs = meter.CreateHistogram<double>("leagues_endpoint_latency_ms", "ms");
	}

	public void RecordFunnelAttempt(string step, string outcome)
	{
		_funnelAttempts.Add(1, new KeyValuePair<string, object?>("step", step),
			new KeyValuePair<string, object?>("outcome", outcome));
	}

	public void RecordEndpointLatency(string endpoint, int statusCode, double latencyMs)
	{
		_endpointLatencyMs.Record(latencyMs, new KeyValuePair<string, object?>("endpoint", endpoint),
			new KeyValuePair<string, object?>("status_code", statusCode));
	}
}