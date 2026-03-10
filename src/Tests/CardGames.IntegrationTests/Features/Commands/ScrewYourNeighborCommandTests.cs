using CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for Screw Your Neighbor KeepOrTrade command.
/// </summary>
public class ScrewYourNeighborCommandTests : IntegrationTestBase
{
	private async Task<(GameSetup Setup, Game Game)> CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(int playerCount = 4)
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "SCREWYOURNEIGHBOR", playerCount, startingChips: 100, ante: 25);

		// Start hand — this triggers dealing and transitions to KeepOrTrade
		await Mediator.Send(new StartHandCommand(setup.Game.Id));

		var game = await DbContext.Games
			.Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
			.Include(g => g.GameCards)
			.FirstAsync(g => g.Id == setup.Game.Id);

		// Should now be in KeepOrTrade phase
		game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));

		return (setup, game);
	}

	[Fact]
	public async Task StartHand_TransitionsToKeepOrTrade()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "SCREWYOURNEIGHBOR", 4, startingChips: 100, ante: 25);

		var result = await Mediator.Send(new StartHandCommand(setup.Game.Id));
		result.IsT0.Should().BeTrue();

		var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
		game.CurrentPhase.Should().Be(nameof(Phases.KeepOrTrade));
	}

	[Fact]
	public async Task KeepOrTrade_KeepDecision_AdvancesToNextPlayer()
	{
		var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

		// Find the first actor (current player whose turn it is)
		var firstActorIndex = game.CurrentPlayerIndex;
		var firstActor = game.GamePlayers
			.FirstOrDefault(gp => gp.SeatPosition == firstActorIndex);
		firstActor.Should().NotBeNull();

		var command = new KeepOrTradeCommand(game.Id, firstActor!.PlayerId, "Keep");
		var result = await Mediator.Send(command);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Decision.Should().Be("Keep");
		success.DidTrade.Should().BeFalse();
	}

	[Fact]
	public async Task KeepOrTrade_TradeDecision_ReturnsSuccess()
	{
		var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

		var firstActorIndex = game.CurrentPlayerIndex;
		var firstActor = game.GamePlayers
			.FirstOrDefault(gp => gp.SeatPosition == firstActorIndex);
		firstActor.Should().NotBeNull();

		var command = new KeepOrTradeCommand(game.Id, firstActor!.PlayerId, "Trade");
		var result = await Mediator.Send(command);

		result.IsT0.Should().BeTrue();
		var success = result.AsT0;
		success.Decision.Should().Be("Trade");
		// Trade may succeed or be blocked if neighbor has King
		(success.DidTrade || success.WasBlocked).Should().BeTrue();
	}

	[Fact]
	public async Task KeepOrTrade_InvalidDecision_ReturnsError()
	{
		var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

		var firstActorIndex = game.CurrentPlayerIndex;
		var firstActor = game.GamePlayers
			.FirstOrDefault(gp => gp.SeatPosition == firstActorIndex);
		firstActor.Should().NotBeNull();

		var command = new KeepOrTradeCommand(game.Id, firstActor!.PlayerId, "InvalidDecision");
		var result = await Mediator.Send(command);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.InvalidDecision);
	}

	[Fact]
	public async Task KeepOrTrade_WrongPlayer_ReturnsError()
	{
		var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync();

		// Get a player who is NOT the current actor
		var firstActorIndex = game.CurrentPlayerIndex;
		var wrongPlayer = game.GamePlayers
			.FirstOrDefault(gp => gp.SeatPosition != firstActorIndex);
		wrongPlayer.Should().NotBeNull();

		var command = new KeepOrTradeCommand(game.Id, wrongPlayer!.PlayerId, "Keep");
		var result = await Mediator.Send(command);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.NotPlayersTurn);
	}

	[Fact]
	public async Task KeepOrTrade_GameNotFound_ReturnsError()
	{
		var command = new KeepOrTradeCommand(Guid.NewGuid(), Guid.NewGuid(), "Keep");
		var result = await Mediator.Send(command);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(KeepOrTradeErrorCode.GameNotFound);
	}

	[Fact]
	public async Task KeepOrTrade_AllPlayersAct_TransitionsToReveal()
	{
		var (setup, game) = await CreateScrewYourNeighborGameInKeepOrTradePhaseAsync(3);

		// All 3 players take their turn (Keep decision for simplicity)
		for (var i = 0; i < 3; i++)
		{
			// Reload game to get updated CurrentPlayerIndex
			game = await DbContext.Games
				.Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
				.Include(g => g.GameCards)
				.AsNoTracking()
				.FirstAsync(g => g.Id == setup.Game.Id);

			// If phase has already transitioned (e.g., due to auto-skip), stop
			if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
				break;

			var currentActor = game.GamePlayers
				.FirstOrDefault(gp => gp.SeatPosition == game.CurrentPlayerIndex);

			if (currentActor is null) break;

			var command = new KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep");
			await Mediator.Send(command);
		}

		// Reload final state
		game = await DbContext.Games.AsNoTracking().FirstAsync(g => g.Id == setup.Game.Id);

		// Should have transitioned past KeepOrTrade (to Reveal or beyond)
		game.CurrentPhase.Should().NotBe(nameof(Phases.KeepOrTrade));
	}
}
