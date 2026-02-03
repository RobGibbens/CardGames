using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using BettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for <see cref="ProcessBettingActionCommandHandler"/>.
/// Tests all betting action types and edge cases.
/// </summary>
public class ProcessBettingActionCommandHandlerTests : IntegrationTestBase
{
    private async Task<(GameSetup Setup, Game Game)> CreateGameInBettingPhaseAsync(int numPlayers = 4)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", numPlayers, ante: 10);
        
        // Start hand
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        
        // Collect antes
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        
        // Deal hands
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Reload game
        var game = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .Include(g => g.BettingRounds.Where(br => !br.IsComplete))
            .Include(g => g.Pots)
            .FirstAsync(g => g.Id == setup.Game.Id);

        return (setup, game);
    }

    [Fact]
    public async Task Handle_GameNotFound_ReturnsError()
    {
        // Arrange
        var command = new ProcessBettingActionCommand(
            Guid.NewGuid(),
            BettingActionType.Check,
            0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.GameNotFound);
    }

    [Fact]
    public async Task Handle_NotInBettingPhase_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var command = new ProcessBettingActionCommand(
            setup.Game.Id,
            BettingActionType.Check,
            0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidGameState);
    }

    [Fact]
    public async Task Handle_FoldAction_MarksPlayerAsFolded()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();

        // First player bets so there is a bet to fold against
        await Mediator.Send(new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Bet,
            10));

        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Fold,
            0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.Action.ActionType.Should().Be(BettingActionType.Fold);

        // Verify player is marked as folded
        var player = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.GameId == game.Id && gp.SeatPosition == success.PlayerSeatIndex);
        player.HasFolded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_CheckAction_WhenNoBet_Succeeds()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Check,
            0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.Action.ActionType.Should().Be(BettingActionType.Check);
    }

    [Fact]
    public async Task Handle_BetAction_UpdatesPotAndCurrentBet()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        var betAmount = 50;
        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Bet,
            betAmount);

        var potBefore = game.Pots.Sum(p => p.Amount);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.Action.ActionType.Should().Be(BettingActionType.Bet);
        success.Action.Amount.Should().Be(betAmount);
        success.PotTotal.Should().Be(potBefore + betAmount);
        success.CurrentBet.Should().Be(betAmount);
    }

    [Fact]
    public async Task Handle_CallAction_MatchesCurrentBet()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        
        // First player bets
        await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 50));

        // Second player calls
        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Call,
            0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.Action.ActionType.Should().Be(BettingActionType.Call);
        success.Action.Amount.Should().Be(50); // Should match the bet
    }

    [Fact]
    public async Task Handle_RaiseAction_IncreasesCurrentBet()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        
        // First player bets 50
        await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 50));

        // Second player raises to 100
        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Raise,
            100);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.Action.ActionType.Should().Be(BettingActionType.Raise);
        success.CurrentBet.Should().Be(100);
    }

    [Fact]
    public async Task Handle_AllInAction_MovesAllChips()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        var bettingRound = game.BettingRounds.First(br => !br.IsComplete);
        var currentPlayer = game.GamePlayers.First(gp => gp.SeatPosition == bettingRound.CurrentActorIndex);
        var chipsBefore = currentPlayer.ChipStack;

        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.AllIn,
            0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.Action.ActionType.Should().Be(BettingActionType.AllIn);
        success.Action.ChipStackAfter.Should().Be(0);

        // Verify player is marked as all-in
        var updatedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.Id == currentPlayer.Id);
        updatedPlayer.IsAllIn.Should().BeTrue();
        updatedPlayer.ChipStack.Should().Be(0);
    }

    [Fact]
    public async Task Handle_CheckWhenBetExists_ReturnsError()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        
        // First player bets
        await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 50));

        // Second player tries to check
        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Check,
            0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
    }

    [Fact]
    public async Task Handle_BetWhenBetExists_ReturnsError()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        
        // First player bets
        await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 50));

        // Second player tries to bet again instead of raise/call
        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Bet,
            75);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
        result.AsT1.Code.Should().Be(ProcessBettingActionErrorCode.InvalidAction);
    }

    [Fact]
    public async Task Handle_RoundComplete_AdvancesToNextPhase()
    {
        // Arrange - Create 2-player game for simpler round completion
        var (setup, game) = await CreateGameInBettingPhaseAsync(numPlayers: 2);

        // Both players check - round should complete
        await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Check, 0));
        var result = await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Check, 0));

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.RoundComplete.Should().BeTrue();
        success.CurrentPhase.Should().NotBe(nameof(Phases.FirstBettingRound));
    }

    [Fact]
    public async Task Handle_ActionCreatesRecord()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Check,
            0);

        // Act
        await Mediator.Send(command);

        // Assert
        var actionRecord = await GetFreshDbContext().BettingActionRecords
            .FirstOrDefaultAsync(bar => bar.BettingRound.GameId == game.Id);
        
        actionRecord.Should().NotBeNull();
        actionRecord!.ActionType.Should().Be(BettingActionType.Check);
        actionRecord.ActionOrder.Should().Be(1);
        actionRecord.IsForced.Should().BeFalse();
        actionRecord.IsTimeout.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UpdatesPlayerCurrentBet()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        var bettingRound = game.BettingRounds.First(br => !br.IsComplete);
        var currentActorIndex = bettingRound.CurrentActorIndex;
        var betAmount = 75;

        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Bet,
            betAmount);

        // Act
        await Mediator.Send(command);

        // Assert
        var player = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.GameId == game.Id && gp.SeatPosition == currentActorIndex);
        player.CurrentBet.Should().Be(betAmount);
        player.TotalContributedThisHand.Should().BeGreaterThanOrEqualTo(betAmount);
    }

    [Fact]
    public async Task Handle_AdvancesToNextPlayer()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync();
        var bettingRound = game.BettingRounds.First(br => !br.IsComplete);
        var initialActorIndex = bettingRound.CurrentActorIndex;

        var command = new ProcessBettingActionCommand(
            game.Id,
            BettingActionType.Check,
            0);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.PlayerSeatIndex.Should().Be(initialActorIndex);
        success.NextPlayerIndex.Should().NotBe(initialActorIndex);
    }

    [Fact]
    public async Task Handle_AllButOneFold_EndsRoundEarly()
    {
        // Arrange
        var (setup, game) = await CreateGameInBettingPhaseAsync(numPlayers: 3);

        // Player 1 bets (to allow others to fold)
        await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Bet, 10));

        // Player 2 folds
        await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Fold, 0));

        // Player 3 folds
        var result = await Mediator.Send(new ProcessBettingActionCommand(game.Id, BettingActionType.Fold, 0));

        // Assert - With only one player remaining, should go to showdown
        result.IsT0.Should().BeTrue();
        var success = result.AsT0;
        success.RoundComplete.Should().BeTrue();
    }
}
