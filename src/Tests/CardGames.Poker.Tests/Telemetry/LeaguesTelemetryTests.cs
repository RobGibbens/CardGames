using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using CardGames.Poker.Api.Features.Leagues.v1.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGames.Poker.Tests.Telemetry;

public class LeaguesTelemetryTests
{
	[Fact]
	public void RecordLeagueEventScheduled_EmitsSourceTypeAndGameTypeTags()
	{
		using var listener = new MeterListener();
		var measurements = new List<CapturedLongMeasurement>();
		ConfigureLongCounterListener(listener, "CardGames.Poker.Api.Leagues", "league_events_scheduled_total", measurements);
		listener.Start();

		using var provider = CreateServiceProvider();
		var telemetry = provider.GetRequiredService<LeaguesTelemetry>();

		telemetry.RecordLeagueEventScheduled("one off", "BASEBALL");

		measurements.Should().ContainSingle();
		measurements[0].Value.Should().Be(1);
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("source_type", "one_off"));
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("game_type", "baseball"));
	}

	private static ServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		services.AddMetrics();
		services.AddSingleton<LeaguesTelemetry>();
		return services.BuildServiceProvider();
	}

	private static void ConfigureLongCounterListener(MeterListener listener, string meterName, string instrumentName,
		List<CapturedLongMeasurement> measurements)
	{
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (instrument.Meter.Name == meterName && instrument.Name == instrumentName)
			{
				meterListener.EnableMeasurementEvents(instrument);
			}
		};

		listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
		{
			measurements.Add(new CapturedLongMeasurement(value, tags.ToArray()));
		});
	}

	private sealed record CapturedLongMeasurement(long Value, KeyValuePair<string, object?>[] Tags);
}
