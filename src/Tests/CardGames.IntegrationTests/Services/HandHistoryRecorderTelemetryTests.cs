using System.Diagnostics.Metrics;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGames.IntegrationTests.Services;

public class HandHistoryRecorderTelemetryTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly HandHistoryTelemetry _telemetry;

    public HandHistoryRecorderTelemetryTests()
    {
        _serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        _telemetry = new HandHistoryTelemetry(_serviceProvider.GetRequiredService<IMeterFactory>());
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task RecordHandHistoryAsync_Success_IncrementsRecordedCounter()
    {
        // Arrange
        using var context = CreateContext();
        var recorder = new HandHistoryRecorder(context, NullLogger<HandHistoryRecorder>.Instance, _telemetry);
        using var listener = CreateCounterListener(out var measurements);
        var parameters = CreateDefaultParameters(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await recorder.RecordHandHistoryAsync(parameters);

        // Assert
        result.Should().BeTrue();
        measurements.Should().Contain(m => HasOutcome(m.Tags, "recorded"));
    }

    [Fact]
    public async Task RecordHandHistoryAsync_Failure_IncrementsFailedCounterAndRethrows()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CardsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var failure = new Exception("Database connection lost");
        using var context = new ThrowingCardsDbContext(options, failure);
        var recorder = new HandHistoryRecorder(context, NullLogger<HandHistoryRecorder>.Instance, _telemetry);
        using var listener = CreateCounterListener(out var measurements);
        var parameters = CreateDefaultParameters(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = async () => await recorder.RecordHandHistoryAsync(parameters);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Database connection lost");
        measurements.Should().Contain(m => HasOutcome(m.Tags, "failed"));
    }

    private static CardsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CardsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CardsDbContext(options);
    }

    private static MeterListener CreateCounterListener(out List<(long Value, KeyValuePair<string, object?>[] Tags)> measurements)
    {
        measurements = [];
        var capturedMeasurements = measurements;
        var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == HandHistoryTelemetry.MeterName &&
                instrument.Name == "hand_history_recorded_total")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
        {
            capturedMeasurements.Add((value, tags.ToArray()));
        });
        listener.Start();
        return listener;
    }

    private static bool HasOutcome(KeyValuePair<string, object?>[] tags, string outcome)
        => tags.Any(tag => tag.Key == "outcome" && Equals(tag.Value, outcome));

    private static RecordHandHistoryParameters CreateDefaultParameters(Guid gameId, Guid playerId)
    {
        return new RecordHandHistoryParameters
        {
            GameId = gameId,
            HandNumber = 1,
            CompletedAtUtc = DateTimeOffset.UtcNow,
            WonByFold = false,
            TotalPot = 100,
            WinningHandDescription = "Two Pair",
            Winners = new List<WinnerInfo>
            {
                new()
                {
                    PlayerId = playerId,
                    PlayerName = "TestPlayer",
                    AmountWon = 100
                }
            },
            PlayerResults = new List<PlayerResultInfo>
            {
                new()
                {
                    PlayerId = playerId,
                    PlayerName = "TestPlayer",
                    SeatPosition = 1,
                    HasFolded = false,
                    ReachedShowdown = true,
                    IsWinner = true,
                    IsSplitPot = false,
                    NetChipDelta = 50,
                    WentAllIn = false,
                    FoldStreet = null,
                    ShowdownCards = new List<string> { "Ah", "Kh" }
                }
            }
        };
    }

    private sealed class ThrowingCardsDbContext : CardsDbContext
    {
        private readonly Exception _exceptionToThrow;

        public ThrowingCardsDbContext(DbContextOptions<CardsDbContext> options, Exception exceptionToThrow) : base(options)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw _exceptionToThrow;
        }
    }
}
