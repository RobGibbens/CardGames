using System.Diagnostics.Metrics;

namespace CardGames.Poker.Api.Features.Leagues.v1.Telemetry;

public sealed class LeaguesTelemetry
{
	private readonly Histogram<double> _endpointLatencyMs;
	private readonly Counter<long> _funnelAttempts;
	private readonly Counter<long> _leagueCreated;
	private readonly Counter<long> _leagueEventsScheduled;

	public LeaguesTelemetry(IMeterFactory meterFactory)
	{
		var meter = meterFactory.Create("CardGames.Poker.Api.Leagues");
		_funnelAttempts = meter.CreateCounter<long>("leagues_funnel_attempts_total");
		_endpointLatencyMs = meter.CreateHistogram<double>("leagues_endpoint_latency_ms", "ms");
		_leagueCreated = meter.CreateCounter<long>("leagues_created_total");
		_leagueEventsScheduled = meter.CreateCounter<long>("league_events_scheduled_total");
	}

	public void RecordFunnelAttempt(string step, string outcome)
	{
		_funnelAttempts.Add(1, new KeyValuePair<string, object?>("step", step),
			new KeyValuePair<string, object?>("outcome", outcome));
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

	/// <summary>
	/// Records a created league.
	/// </summary>
	public void RecordLeagueCreated()
	{
		_leagueCreated.Add(1);
	}

	/// <summary>
	/// Records a scheduled league event.
	/// </summary>
	public void RecordLeagueEventScheduled(string sourceType, string? gameTypeCode)
	{
		_leagueEventsScheduled.Add(1,
			new("source_type", Normalize(sourceType, "unknown")),
			new("game_type", Normalize(gameTypeCode, "unknown")));
	}

	private static string Normalize(string? value, string fallback)
	{
		return string.IsNullOrWhiteSpace(value)
			? fallback
			: value.Trim().ToLowerInvariant().Replace(' ', '_');
	}
}