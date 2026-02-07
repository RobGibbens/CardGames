using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CardGames.IntegrationTests.Services;

public class HandHistoryRecorderTests : IDisposable
{
    private readonly CardsDbContext _context;
    private readonly HandHistoryRecorder _recorder;

    public HandHistoryRecorderTests()
    {
        var options = new DbContextOptionsBuilder<CardsDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new CardsDbContext(options);
        _recorder = new HandHistoryRecorder(_context, NullLogger<HandHistoryRecorder>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public async Task RecordHandHistoryAsync_ValidData_RecordsHandHistory()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var parameters = CreateDefaultParameters(gameId, playerId);

        // Act
        var result = await _recorder.RecordHandHistoryAsync(parameters);

        // Assert
        result.Should().BeTrue();

        var history = await _context.HandHistories
            .Include(h => h.Winners)
            .Include(h => h.PlayerResults)
            .FirstOrDefaultAsync(h => h.GameId == gameId && h.HandNumber == 1);

        history.Should().NotBeNull();
        history!.TotalPot.Should().Be(100);
        history.WinningHandDescription.Should().Be("Two Pair");
        history.Winners.Should().ContainSingle();
        history.Winners.First().PlayerId.Should().Be(playerId);
        history.PlayerResults.Should().HaveCount(1);
        
        var playerResult = history.PlayerResults.First();
        playerResult.PlayerId.Should().Be(playerId);
        playerResult.ShowdownCards.Should().Contain("Ah").And.Contain("Kh");
    }

    [Fact]
    public async Task RecordHandHistoryAsync_DuplicateRecord_ReturnsFalse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var parameters = CreateDefaultParameters(gameId, Guid.NewGuid());
        
        // Record first time
        await _recorder.RecordHandHistoryAsync(parameters);

        // Act
        var result = await _recorder.RecordHandHistoryAsync(parameters);

        // Assert
        result.Should().BeFalse();
        _context.HandHistories.Count().Should().Be(1);
    }

    [Fact]
    public async Task RecordHandHistoryAsync_ConcurrentInsert_ReturnsFalse()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CardsDbContext>()
             .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
             .Options;
        
        // Use a DbUpdateException that mimics a unique constraint violation
        var innerEx = new Exception("Violation of UNIQUE KEY constraint 'IX_HandHistories_GameId_HandNumber'. Cannot insert duplicate key in object 'dbo.HandHistories'.");
        var dbUpdateEx = new DbUpdateException("An error occurred while saving the entity changes.", innerEx);
        
        using var throwingContext = new ThrowingCardsDbContext(options, dbUpdateEx);
        // We need to bypass the initial "AnyAsync" check, or ensure it doesn't fail.
        // With an empty throwing context, AnyAsync returns false (record doesn't exist yet).
        // Then SaveChangesAsync throws.
        
        var recorder = new HandHistoryRecorder(throwingContext, NullLogger<HandHistoryRecorder>.Instance);
        var parameters = CreateDefaultParameters(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await recorder.RecordHandHistoryAsync(parameters);

        // Assert
        result.Should().BeFalse("Concurrent insert (simulated by exception) should return false (success)");
    }

    [Fact]
    public async Task RecordHandHistoryAsync_GeneralException_ReturnsFalseAndLogs()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<CardsDbContext>()
             .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
             .Options;
        
        var generalEx = new Exception("Database connection lost");
        
        using var throwingContext = new ThrowingCardsDbContext(options, generalEx);
        var recorder = new HandHistoryRecorder(throwingContext, NullLogger<HandHistoryRecorder>.Instance);
        var parameters = CreateDefaultParameters(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = await recorder.RecordHandHistoryAsync(parameters);

        // Assert
        result.Should().BeFalse("General exception returns false");
    }

    [Theory]
    [InlineData("PREFLOP", FoldStreet.Preflop)]
    [InlineData("FLOP", FoldStreet.Flop)]
    [InlineData("TURN", FoldStreet.Turn)]
    [InlineData("RIVER", FoldStreet.River)]
    [InlineData("FIRSTBETTINGROUND", FoldStreet.FirstRound)]
    [InlineData("DRAWPHASE", FoldStreet.DrawPhase)]
    [InlineData("SECONDBETTINGROUND", FoldStreet.SecondRound)]
    [InlineData("THIRDSTREET", FoldStreet.ThirdStreet)]
    [InlineData("FOURTHSTREET", FoldStreet.FourthStreet)]
    [InlineData("FIFTHSTREET", FoldStreet.FifthStreet)]
    [InlineData("SIXTHSTREET", FoldStreet.SixthStreet)]
    [InlineData("SEVENTHSTREET", FoldStreet.SeventhStreet)]
    [InlineData(null, null)]
    public async Task RecordHandHistoryAsync_MapsFoldStreetCorrectly(string? inputStreet, FoldStreet? expectedStreet)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var defaultParams = CreateDefaultParameters(gameId, playerId);
        
        // Modify the one player result
        var playerResult = defaultParams.PlayerResults.First();
        var newPlayerResults = new List<PlayerResultInfo>
        {
             new PlayerResultInfo
             {
                 PlayerId = playerResult.PlayerId,
                 PlayerName = playerResult.PlayerName,
                 SeatPosition = playerResult.SeatPosition,
                 NetChipDelta = playerResult.NetChipDelta,
                 FoldStreet = inputStreet, 
                 HasFolded = inputStreet != null,
                 IsWinner = false,
                 ReachedShowdown = false,
                 IsSplitPot = false,
                 WentAllIn = false,
                 ShowdownCards = null
             }
        };
        
        var newParams = new RecordHandHistoryParameters
        {
            GameId = defaultParams.GameId,
            HandNumber = defaultParams.HandNumber,
            CompletedAtUtc = defaultParams.CompletedAtUtc,
            WonByFold = defaultParams.WonByFold,
            TotalPot = defaultParams.TotalPot,
            WinningHandDescription = defaultParams.WinningHandDescription,
            Winners = defaultParams.Winners,
            PlayerResults = newPlayerResults
        };

        // Act
        await _recorder.RecordHandHistoryAsync(newParams);

        // Assert
        var history = await _context.HandHistories.Include(h => h.PlayerResults).FirstAsync();
        history.PlayerResults.First().FoldStreet.Should().Be(expectedStreet);
    }
    
    [Theory]
    [InlineData(true, false, false, PlayerResultType.Folded)]
    [InlineData(false, true, false, PlayerResultType.Won)]
    [InlineData(false, true, true, PlayerResultType.SplitPotWon)]
    [InlineData(false, false, false, PlayerResultType.Lost)]
    public async Task RecordHandHistoryAsync_MapsResultTypeCorrectly(bool hasFolded, bool isWinner, bool isSplitPot, PlayerResultType expectedType)
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var defaultParams = CreateDefaultParameters(gameId, playerId);
        
        var newPlayerResults = new List<PlayerResultInfo>
        {
             new PlayerResultInfo
             {
                 PlayerId = playerId,
                 PlayerName = "Player",
                 SeatPosition = 1,
                 NetChipDelta = 0,
                 FoldStreet = hasFolded ? "PREFLOP" : null,
                 HasFolded = hasFolded,
                 IsWinner = isWinner,
                 ReachedShowdown = !hasFolded,
                 IsSplitPot = isSplitPot,
                 WentAllIn = false,
                 ShowdownCards = null
             }
        };
        
        var newParams = new RecordHandHistoryParameters
        {
            GameId = defaultParams.GameId,
            HandNumber = defaultParams.HandNumber,
            CompletedAtUtc = defaultParams.CompletedAtUtc,
            WonByFold = defaultParams.WonByFold,
            TotalPot = defaultParams.TotalPot,
            WinningHandDescription = defaultParams.WinningHandDescription,
            Winners = defaultParams.Winners,
            PlayerResults = newPlayerResults
        };

        // Act
        await _recorder.RecordHandHistoryAsync(newParams);

        // Assert
        var history = await _context.HandHistories.Include(h => h.PlayerResults).FirstAsync();
        history.PlayerResults.First().ResultType.Should().Be(expectedType);
    }
    
    private RecordHandHistoryParameters CreateDefaultParameters(Guid gameId, Guid playerId)
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
                new WinnerInfo
                {
                    PlayerId = playerId,
                    PlayerName = "TestPlayer",
                    AmountWon = 100
                }
            },
            PlayerResults = new List<PlayerResultInfo>
            {
                new PlayerResultInfo
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

    private class ThrowingCardsDbContext : CardsDbContext
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
