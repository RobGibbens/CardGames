using System.Diagnostics.Metrics;
using CardGames.Poker.Api.Features.Leagues.v1.Telemetry;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGames.IntegrationTests.Services;

public class LeaguesTelemetryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly LeaguesTelemetry _telemetry;

    public LeaguesTelemetryTests()
    {
        _serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _telemetry = new LeaguesTelemetry(_serviceProvider.GetRequiredService<IMeterFactory>());
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Theory]
    [InlineData(200, "success")]
    [InlineData(204, "success")]
    [InlineData(302, "success")]
    [InlineData(400, "error")]
    [InlineData(404, "error")]
    [InlineData(500, "error")]
    public void RecordEndpointLatency_EmitsOutcomeTagDerivedFromStatusCode(int statusCode, string expectedOutcome)
    {
        using var listener = CreateHistogramListener(out var measurements);

        _telemetry.RecordEndpointLatency("join_request", statusCode, 12.5);

        measurements.Should().ContainSingle();
        var measurement = measurements[0];
        measurement.Value.Should().Be(12.5);
        TagValue(measurement.Tags, "endpoint").Should().Be("join_request");
        TagValue(measurement.Tags, "status_code").Should().Be(statusCode);
        TagValue(measurement.Tags, "outcome").Should().Be(expectedOutcome);
    }

    private static MeterListener CreateHistogramListener(out List<(double Value, KeyValuePair<string, object?>[] Tags)> measurements)
    {
        measurements = [];
        var capturedMeasurements = measurements;
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "CardGames.Poker.Api.Leagues" &&
                instrument.Name == "leagues_endpoint_latency_ms")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
        {
            capturedMeasurements.Add((value, tags.ToArray()));
        });
        listener.Start();
        return listener;
    }

    private static object? TagValue(KeyValuePair<string, object?>[] tags, string key)
        => tags.FirstOrDefault(tag => tag.Key == key).Value;
}
