using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Features.Leagues.v1.Telemetry;

public sealed class LeaguesTelemetry
{
	private readonly Counter<long> _funnelAttempts;
	private readonly Histogram<double> _endpointLatencyMs;

	public LeaguesTelemetry(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create("CardGames.Poker.Api.Leagues");
		_funnelAttempts = meter.CreateCounter<long>("leagues_funnel_attempts_total");
		_endpointLatencyMs = meter.CreateHistogram<double>("leagues_endpoint_latency_ms", unit: "ms");
	}

	public void RecordFunnelAttempt(string step, string outcome)
	{
		_funnelAttempts.Add(1, new("step", step), new("outcome", outcome));
	}

	public void RecordEndpointLatency(string endpoint, int statusCode, double latencyMs)
	{
		_endpointLatencyMs.Record(latencyMs, new("endpoint", endpoint), new("status_code", statusCode));
	}
}
