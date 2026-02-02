using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Handlers;

/// <summary>
/// Integration tests for <see cref="StartHandCommandHandler"/>.
/// Tests the generic handler that delegates to game-specific flow handlers.
/// </summary>
public class StartHandCommandHandlerIntegrationTests : IntegrationTestBase
{
    [Theory]
    [InlineData("FIVECARDDRAW", "CollectingAntes")]
    [InlineData("SEVENCARDSTUD", "CollectingAntes")]
    [InlineData("KINGSANDLOWS", "Dealing")]
    [InlineData("TWOSJACKSMANWITHTHEAXE", "CollectingAntes")]
    public async Task Handle_ValidGame_TransitionsToCorrectInitialPhase(string gameTypeCode, string expectedPhase)
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, gameTypeCode, 4);
        var command = new StartHandCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue("Expected successful result");
        var success = result.AsT0;
        success.CurrentPhase.Should().Be(expectedPhase);
        success.HandNumber.Should().Be(1);
        success.ActivePlayerCount.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ValidGame_IncreasesHandNumber()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.CurrentHandNumber = 5;
        await DbContext.SaveChangesAsync();
        var command = new StartHandCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        result.AsT0.HandNumber.Should().Be(6);

        // Verify in database
        var game = await GetFreshDbContext().Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.CurrentHandNumber.Should().Be(6);
    }

    [Fact]
    public async Task Handle_ValidGame_UpdatesGameStatus()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var command = new StartHandCommand(setup.Game.Id);

        // Act
        await Mediator.Send(command);

        // Assert - Game status should change to InProgress
        var game = await GetFreshDbContext().Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.Status.Should().Be(GameStatus.InProgress);
    }

    [Fact]
    public async Task Handle_ValidGame_CreatesMainPot()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var command = new StartHandCommand(setup.Game.Id);

        // Act
        await Mediator.Send(command);

        // Assert - A main pot should be created for the new hand
        var pots = await GetFreshDbContext().Pots
            .Where(p => p.GameId == setup.Game.Id && p.HandNumber == 1)
            .ToListAsync();

        pots.Should().HaveCount(1);
        pots[0].PotType.Should().Be(PotType.Main);
        pots[0].Amount.Should().Be(0); // Antes not yet collected
        pots[0].IsAwarded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_GameNotFound_ReturnsError()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var command = new StartHandCommand(nonExistentId);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue("Expected error result");
        var error = result.AsT1;
        error.Code.Should().Be(StartHandErrorCode.GameNotFound);
        error.Message.Should().Contain(nonExistentId.ToString());
    }

    [Fact]
    public async Task Handle_GameInProgress_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.CurrentPhase = nameof(Phases.FirstBettingRound);
        await DbContext.SaveChangesAsync();
        var command = new StartHandCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue("Expected error result");
        var error = result.AsT1;
        error.Code.Should().Be(StartHandErrorCode.InvalidGameState);
    }

    [Fact]
    public async Task Handle_NotEnoughPlayers_ReturnsError()
    {
        // Arrange - Create game with only 1 player
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 1);
        var command = new StartHandCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue("Expected error result");
        var error = result.AsT1;
        error.Code.Should().Be(StartHandErrorCode.NotEnoughPlayers);
    }

    [Fact]
    public async Task Handle_AllPlayersSittingOut_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        foreach (var gp in setup.GamePlayers)
        {
            gp.IsSittingOut = true;
        }
        await DbContext.SaveChangesAsync();
        var command = new StartHandCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(StartHandErrorCode.NotEnoughPlayers);
    }

    [Fact]
    public async Task Handle_PlayersWithInsufficientChips_AutoSitsOut()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext, "FIVECARDDRAW", 4, startingChips: 1000, ante: 10);
        
        // Set one player with insufficient chips (less than ante)
        setup.GamePlayers[3].ChipStack = 5;
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert - Should succeed with 3 players
        result.IsT0.Should().BeTrue();
        result.AsT0.ActivePlayerCount.Should().Be(3);

        // Verify the player with insufficient chips was sat out
        var updatedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == setup.GamePlayers[3].Id);
        updatedPlayer.IsSittingOut.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AppliesPendingChips()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        
        // Add pending chips to one player
        setup.GamePlayers[0].ChipStack = 100;
        setup.GamePlayers[0].PendingChipsToAdd = 500;
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);

        // Act
        await Mediator.Send(command);

        // Assert - Pending chips should be applied
        var updatedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == setup.GamePlayers[0].Id);
        updatedPlayer.ChipStack.Should().Be(600);
        updatedPlayer.PendingChipsToAdd.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ProcessesPendingLeaveRequests()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        
        // Mark one player as wanting to leave after current hand
        setup.GamePlayers[3].LeftAtHandNumber = 0; // Leave after hand 0
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert - Should succeed with 3 remaining players
        result.IsT0.Should().BeTrue();
        result.AsT0.ActivePlayerCount.Should().Be(3);

        // Verify player status changed to Left
        var leftPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == setup.GamePlayers[3].Id);
        leftPlayer.Status.Should().Be(GamePlayerStatus.Left);
    }

    [Fact]
    public async Task Handle_ResetsPlayerStates()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        
        // Set various player states from previous hand
        foreach (var gp in setup.GamePlayers)
        {
            gp.CurrentBet = 50;
            gp.TotalContributedThisHand = 100;
            gp.HasFolded = true;
            gp.IsAllIn = true;
        }
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);

        // Act
        await Mediator.Send(command);

        // Assert - All player states should be reset
        var freshContext = GetFreshDbContext();
        var gamePlayers = await freshContext.GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .ToListAsync();

        gamePlayers.Should().AllSatisfy(gp =>
        {
            gp.CurrentBet.Should().Be(0);
            gp.TotalContributedThisHand.Should().Be(0);
            gp.HasFolded.Should().BeFalse();
            gp.IsAllIn.Should().BeFalse();
        });
    }

    [Fact]
    public async Task Handle_RemovesPreviousHandCards()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        
        // Add some cards from a previous hand
        var previousCards = new List<GameCard>
        {
            new() { GameId = setup.Game.Id, HandNumber = 0, Suit = CardSuit.Hearts, Symbol = CardSymbol.Ace, Location = CardLocation.Hand },
            new() { GameId = setup.Game.Id, HandNumber = 0, Suit = CardSuit.Clubs, Symbol = CardSymbol.King, Location = CardLocation.Hand }
        };
        DbContext.GameCards.AddRange(previousCards);
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);

        // Act
        await Mediator.Send(command);

        // Assert - Previous cards should be removed
        var remainingCards = await GetFreshDbContext().GameCards
            .Where(gc => gc.GameId == setup.Game.Id && gc.HandNumber == 0)
            .ToListAsync();
        remainingCards.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_KingsAndLows_ResetsDropOrStayDecisions()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        
        // Set DropOrStay decisions from previous hand
        foreach (var gp in setup.GamePlayers)
        {
            gp.DropOrStayDecision = DropOrStayDecision.Stay;
        }
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);

        // Act
        await Mediator.Send(command);

        // Assert - DropOrStay decisions should be reset
        var gamePlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .ToListAsync();

        gamePlayers.Should().AllSatisfy(gp =>
        {
            gp.DropOrStayDecision.Should().Be(DropOrStayDecision.Undecided);
        });
    }

    [Fact]
    public async Task Handle_FromCompletedHand_StartsNextHand()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.CurrentPhase = nameof(Phases.Complete);
        setup.Game.CurrentHandNumber = 3;
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        result.AsT0.HandNumber.Should().Be(4);
        result.AsT0.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
    }

    [Fact]
    public async Task Handle_SetsStartedAtOnFirstHand()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.StartedAt = null; // Ensure not set
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);
        var beforeStart = DateTimeOffset.UtcNow;

        // Act
        await Mediator.Send(command);
        var afterStart = DateTimeOffset.UtcNow;

        // Assert
        var game = await GetFreshDbContext().Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.StartedAt.Should().NotBeNull();
        game.StartedAt.Should().BeOnOrAfter(beforeStart);
        game.StartedAt.Should().BeOnOrBefore(afterStart);
    }

    [Fact]
    public async Task Handle_DoesNotOverwriteStartedAtOnSubsequentHands()
    {
        // Arrange
        var originalStartTime = DateTimeOffset.UtcNow.AddHours(-1);
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        setup.Game.StartedAt = originalStartTime;
        setup.Game.CurrentPhase = nameof(Phases.Complete);
        setup.Game.CurrentHandNumber = 1;
        await DbContext.SaveChangesAsync();

        var command = new StartHandCommand(setup.Game.Id);

        // Act
        await Mediator.Send(command);

        // Assert - StartedAt should not change
        var game = await GetFreshDbContext().Games.FirstAsync(g => g.Id == setup.Game.Id);
        game.StartedAt.Should().Be(originalStartTime);
    }
}
