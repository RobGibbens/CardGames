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

	/// <summary>
	/// Records endpoint latency for a Leagues endpoint.
	/// </summary>
	/// <param name="endpoint">
	/// A low-cardinality, stable route template or logical endpoint name (e.g. <c>join_request</c>
	/// or <c>leagues/{id}/join</c>). Never pass a concrete path with a league/user id interpolated,
	/// as that would explode the cardinality of the <c>endpoint</c> tag.
	/// </param>
	/// <param name="statusCode">The HTTP status code. Bounded/low-cardinality.</param>
	/// <param name="latencyMs">The measured latency in milliseconds.</param>
	public void RecordEndpointLatency(string endpoint, int statusCode, double latencyMs)
	{
		var outcome = statusCode is >= 200 and < 400 ? "success" : "error";
		_endpointLatencyMs.Record(
			latencyMs,
			new("endpoint", endpoint),
			new("status_code", statusCode),
			new("outcome", outcome));
	}
}
