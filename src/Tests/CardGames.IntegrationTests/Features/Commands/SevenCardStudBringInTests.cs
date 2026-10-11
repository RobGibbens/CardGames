using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.SevenCardStud.v1.Commands.StartHand;

namespace CardGames.IntegrationTests.Features.Commands;

public class SevenCardStudBringInTests : IntegrationTestBase
{
	[Fact]
	public async Task DealHands_ThirdStreet_PostsBringInIntoPot()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "SEVENCARDSTUD", 2, startingChips: 1000, ante: 5);
		setup.Game.MinBet = 10;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
		var result = await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		result.IsT0.Should().BeTrue();

		var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
		game.BringIn.Should().Be(5);
		game.SmallBet.Should().Be(10);
		game.BigBet.Should().Be(20);

		var players = await DbContext.GamePlayers.Where(p => p.GameId == setup.Game.Id).ToListAsync();
		var bringInPlayer = players.Single(p => p.CurrentBet == 5);
		bringInPlayer.ChipStack.Should().Be(990);
		bringInPlayer.TotalContributedThisHand.Should().Be(10);
		bringInPlayer.SeatPosition.Should().Be(game.BringInPlayerIndex);
		players.Count(p => p.CurrentBet == 0).Should().Be(1);

		var potTotal = await DbContext.Pots
			.Where(p => p.GameId == setup.Game.Id && p.HandNumber == game.CurrentHandNumber && p.PotType == PotType.Main)
			.SumAsync(p => p.Amount);
		potTotal.Should().Be(15);

		var contributionTotal = await DbContext.PotContributions
			.Where(c => c.GamePlayerId == bringInPlayer.Id)
			.SumAsync(c => c.Amount);
		contributionTotal.Should().Be(10);

		var bettingRound = await DbContext.BettingRounds
			.SingleAsync(br => br.GameId == setup.Game.Id && br.Street == nameof(Phases.ThirdStreet));
		bettingRound.CurrentBet.Should().Be(5);
		bettingRound.CurrentActorIndex.Should().Be(bringInPlayer.SeatPosition);
	}

	[Fact]
	public async Task DealHands_ThirdStreet_LegacyNullBringIn_NormalizesThenPosts()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "SEVENCARDSTUD", 2, startingChips: 1000, ante: 5);
		setup.Game.MinBet = 10;
		setup.Game.BringIn = null;
		setup.Game.SmallBet = null;
		setup.Game.BigBet = null;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
		var result = await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var game = await DbContext.Games.FirstAsync(g => g.Id == setup.Game.Id);
		game.BringIn.Should().Be(5);
		game.SmallBet.Should().Be(10);
		game.BigBet.Should().Be(20);

		var players = await DbContext.GamePlayers.Where(p => p.GameId == setup.Game.Id).ToListAsync();
		players.Should().ContainSingle(p => p.CurrentBet == 5);
	}

	[Fact]
	public async Task DealHands_ThirdStreet_ShortStack_PostsRemainderAndMarksAllIn()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, "SEVENCARDSTUD", 2, startingChips: 1000, ante: 5);
		setup.Game.MinBet = 10;
		await DbContext.SaveChangesAsync();

		await Mediator.Send(new StartHandCommand(setup.Game.Id));
		await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

		var players = await DbContext.GamePlayers.Where(p => p.GameId == setup.Game.Id).ToListAsync();
		foreach (var player in players)
			player.ChipStack = 3;
		await DbContext.SaveChangesAsync();

		var result = await Mediator.Send(new DealHandsCommand(setup.Game.Id));

		result.IsT0.Should().BeTrue();
		var updated = await DbContext.GamePlayers.Where(p => p.GameId == setup.Game.Id).ToListAsync();
		var bringInPlayer = updated.Single(p => p.IsAllIn);
		bringInPlayer.CurrentBet.Should().Be(3);
		bringInPlayer.ChipStack.Should().Be(0);
		updated.Single(p => p.Id != bringInPlayer.Id).ChipStack.Should().Be(3);

		var potTotal = await DbContext.Pots
			.Where(p => p.GameId == setup.Game.Id && p.PotType == PotType.Main)
			.SumAsync(p => p.Amount);
		potTotal.Should().Be(13);
	}
}
