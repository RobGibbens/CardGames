using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGames.Poker.Tests.Telemetry;

public class AccountTelemetryTests
{
	[Fact]
	public void RecordUserCreated_EmitsAccountTypeAndProviderTags()
	{
		using var listener = new MeterListener();
		var measurements = new List<CapturedLongMeasurement>();
		ConfigureLongCounterListener(listener, AccountTelemetry.MeterName, "identity_users_created_total", measurements);
		listener.Start();

		using var provider = CreateServiceProvider();
		var telemetry = provider.GetRequiredService<AccountTelemetry>();

		telemetry.RecordUserCreated("social", "Google", requiresConfirmation: true);

		measurements.Should().ContainSingle();
		measurements[0].Value.Should().Be(1);
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("account_type", "social"));
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("provider", "google"));
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("requires_confirmation", true));
	}

	[Fact]
	public void RecordSignInAttempt_EmitsNormalizedOutcome()
	{
		using var listener = new MeterListener();
		var measurements = new List<CapturedLongMeasurement>();
		ConfigureLongCounterListener(listener, AccountTelemetry.MeterName, "identity_sign_in_attempts_total", measurements);
		listener.Start();

		using var provider = CreateServiceProvider();
		var telemetry = provider.GetRequiredService<AccountTelemetry>();

		telemetry.RecordSignInAttempt("passkey", "requires two factor", "local");

		measurements.Should().ContainSingle();
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("method", "passkey"));
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("outcome", "requires_two_factor"));
		measurements[0].Tags.Should().Contain(new KeyValuePair<string, object?>("provider", "local"));
	}

	private static ServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		services.AddMetrics();
		services.AddSingleton<AccountTelemetry>();
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
