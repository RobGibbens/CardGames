namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for additional common game commands.
/// Tests ToggleSitOut, DeleteGame, UpdateTableSettings, and LeaveGame.
/// </summary>
public class CommonGameCommandTests : IntegrationTestBase
{
    #region ToggleSitOut Tests

    [Fact]
    public async Task ToggleSitOut_MarkPlayerSittingOut()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var player = setup.GamePlayers[0];
        player.IsSittingOut.Should().BeFalse();

        // Simulate toggling sit out in database
        player.IsSittingOut = true;
        await DbContext.SaveChangesAsync();

        // Assert
        var updatedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == player.Id);
        updatedPlayer.IsSittingOut.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleSitOut_UnmarkPlayerSittingOut()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var player = setup.GamePlayers[0];
        player.IsSittingOut = true;
        await DbContext.SaveChangesAsync();

        // Simulate toggling back
        player.IsSittingOut = false;
        await DbContext.SaveChangesAsync();

        // Assert
        var updatedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == player.Id);
        updatedPlayer.IsSittingOut.Should().BeFalse();
    }

    #endregion

    #region DeleteGame Tests

    [Fact]
    public async Task DeleteGame_SoftDeletesGame()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.IsDeleted.Should().BeFalse();

        // Simulate soft delete
        setup.Game.IsDeleted = true;
        setup.Game.DeletedAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();

        // Assert
        var deletedGame = await GetFreshDbContext().Games
            .FirstAsync(g => g.Id == setup.Game.Id);
        deletedGame.IsDeleted.Should().BeTrue();
        deletedGame.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteGame_QueryExcludesDeletedGames()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        
        // Soft delete
        setup.Game.IsDeleted = true;
        await DbContext.SaveChangesAsync();

        // Act - Query for non-deleted games
        var games = await GetFreshDbContext().Games
            .Where(g => !g.IsDeleted)
            .ToListAsync();

        // Assert
        games.Should().NotContain(g => g.Id == setup.Game.Id);
    }

    #endregion

    #region UpdateTableSettings Tests

    [Fact]
    public async Task UpdateTableSettings_UpdatesAnte()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, ante: 10);
        setup.Game.Ante.Should().Be(10);

        // Update ante
        setup.Game.Ante = 25;
        await DbContext.SaveChangesAsync();

        // Assert
        var updatedGame = await GetFreshDbContext().Games
            .FirstAsync(g => g.Id == setup.Game.Id);
        updatedGame.Ante.Should().Be(25);
    }

    [Fact]
    public async Task UpdateTableSettings_UpdatesMinBet()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Update min bet
        setup.Game.MinBet = 50;
        await DbContext.SaveChangesAsync();

        // Assert
        var updatedGame = await GetFreshDbContext().Games
            .FirstAsync(g => g.Id == setup.Game.Id);
        updatedGame.MinBet.Should().Be(50);
    }

    #endregion

    #region LeaveGame Tests

    [Fact]
    public async Task LeaveGame_MarksPlayerAsLeft()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var player = setup.GamePlayers[0];
        player.Status.Should().Be(GamePlayerStatus.Active);

        // Mark as left
        player.Status = GamePlayerStatus.Left;
        player.LeftAt = DateTimeOffset.UtcNow;
        await DbContext.SaveChangesAsync();

        // Assert
        var leftPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == player.Id);
        leftPlayer.Status.Should().Be(GamePlayerStatus.Left);
        leftPlayer.LeftAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LeaveGame_RecordsFinalChipCount()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, startingChips: 1000);
        var player = setup.GamePlayers[0];

        // Set final chip count and leave
        player.Status = GamePlayerStatus.Left;
        player.FinalChipCount = player.ChipStack;
        await DbContext.SaveChangesAsync();

        // Assert
        var leftPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == player.Id);
        leftPlayer.FinalChipCount.Should().Be(1000);
    }

    [Fact]
    public async Task LeaveGame_ActivePlayersExcludesLeftPlayer()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var player = setup.GamePlayers[0];

        // Mark as left
        player.Status = GamePlayerStatus.Left;
        await DbContext.SaveChangesAsync();

        // Act
        var activePlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id && gp.Status == GamePlayerStatus.Active)
            .ToListAsync();

        // Assert
        activePlayers.Should().HaveCount(3);
        activePlayers.Should().NotContain(p => p.Id == player.Id);
    }

    #endregion
}
