using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGames.Poker.Tests.Telemetry;

public class GameEventsTelemetryTests
{
	[Fact]
	public void RecordGameCreated_EmitsNormalizedTags()
	{
		using var listener = new MeterListener();
		var measurements = new List<CapturedLongMeasurement>();
		ConfigureLongCounterListener(listener, GameEventsTelemetry.MeterName, "games_created_total", measurements);
		listener.Start();

		using var provider = CreateServiceProvider();
		var telemetry = provider.GetRequiredService<GameEventsTelemetry>();

		telemetry.RecordGameCreated("Texas Hold'Em", "Dealers Choice");

		measurements.Should().ContainSingle();
		measurements[0].Value.Should().Be(1);
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("game_type", "texas_hold_em"));
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("table_kind", "dealers_choice"));
	}

	[Fact]
	public void RecordHandDuration_EmitsHistogramMeasurement()
	{
		using var listener = new MeterListener();
		var measurements = new List<CapturedDoubleMeasurement>();
		ConfigureDoubleHistogramListener(listener, GameEventsTelemetry.MeterName, "hand_duration_ms", measurements);
		listener.Start();

		using var provider = CreateServiceProvider();
		var telemetry = provider.GetRequiredService<GameEventsTelemetry>();

		telemetry.RecordHandDuration("HOLDEM", "standard", 1234.5);

		measurements.Should().ContainSingle();
		measurements[0].Value.Should().Be(1234.5);
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("game_type", "holdem"));
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("table_kind", "standard"));
	}

	private static ServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		services.AddMetrics();
		services.AddSingleton<GameEventsTelemetry>();
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
			measurements.Add(new CapturedLongMeasurement(instrument.Name, value, tags.ToArray()));
		});
	}

	private static void ConfigureDoubleHistogramListener(MeterListener listener, string meterName, string instrumentName,
		List<CapturedDoubleMeasurement> measurements)
	{
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (instrument.Meter.Name == meterName && instrument.Name == instrumentName)
			{
				meterListener.EnableMeasurementEvents(instrument);
			}
		};

		listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
		{
			measurements.Add(new CapturedDoubleMeasurement(instrument.Name, value, tags.ToArray()));
		});
	}

	private sealed record CapturedLongMeasurement(string InstrumentName, long Value,
		KeyValuePair<string, object?>[] Tags);

	private sealed record CapturedDoubleMeasurement(string InstrumentName, double Value,
		KeyValuePair<string, object?>[] Tags);
}
