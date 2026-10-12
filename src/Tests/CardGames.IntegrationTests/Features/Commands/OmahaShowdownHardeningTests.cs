using CardGames.IntegrationTests.Infrastructure.Fakes;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Evaluation;
using Microsoft.Extensions.Logging.Abstractions;

namespace CardGames.IntegrationTests.Features.Commands;

public class OmahaShowdownHardeningTests : IntegrationTestBase
{
	[Fact]
	public async Task Handle_OmahaShowdown_UsesExactlyTwoHoleCardsForWinnerResolution()
	{
		// Arrange
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext,
			PokerGameMetadataRegistry.OmahaCode,
			2,
			1000,
			0);

		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;
		await DbContext.SaveChangesAsync();

		await DatabaseSeeder.CreatePotAsync(DbContext, game, 100);

		var playerOne = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
		var playerTwo = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);
		playerOne.TotalContributedThisHand = 50;
		playerTwo.TotalContributedThisHand = 50;
		var dealtAt = DateTimeOffset.UtcNow;

		// Player 1 cannot use board straight in Omaha (no two connecting hole cards).
		DbContext.GameCards.AddRange(
			CreateHoleCard(game.Id, playerOne.Id, CardSuit.Spades, CardSymbol.Ace, 1, dealtAt),
			CreateHoleCard(game.Id, playerOne.Id, CardSuit.Diamonds, CardSymbol.Ace, 2, dealtAt),
			CreateHoleCard(game.Id, playerOne.Id, CardSuit.Clubs, CardSymbol.King, 3, dealtAt),
			CreateHoleCard(game.Id, playerOne.Id, CardSuit.Hearts, CardSymbol.Queen, 4, dealtAt),

			// Player 2 can make a straight only when evaluator considers all 4 hole cards.
			// If generic showdown truncates to first 2 cards, Player 2 loses this hand.
			CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Hearts, CardSymbol.Ace, 1, dealtAt),
			CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Spades, CardSymbol.King, 2, dealtAt),
			CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Clubs, CardSymbol.Four, 3, dealtAt),
			CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Diamonds, CardSymbol.Five, 4, dealtAt),

			// Shared board: 6-7-8-9-T
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Six, 1, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Clubs, CardSymbol.Seven, 2, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Diamonds, CardSymbol.Eight, 3, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Spades, CardSymbol.Nine, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Ten, 5, dealtAt));

		await DbContext.SaveChangesAsync();

		var handler = new PerformShowdownCommandHandler(
			DbContext,
			FlowHandlerFactory,
			new HandEvaluatorFactory(),
			new FakeHandHistoryRecorder(),
			new FakeHandSettlementService(),
			NullLogger<PerformShowdownCommandHandler>.Instance);

		// Act
		var result = await handler.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		// Assert
		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts.Should().ContainKey("Player 2");
		result.AsT0.Payouts["Player 2"].Should().Be(100);
		result.AsT0.Payouts.Should().NotContainKey("Player 1");
	}

	[Fact]
	public async Task Handle_OmahaTiedShowdown_OddChipGoesToFirstSeatAfterButton()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext,
			PokerGameMetadataRegistry.OmahaCode,
			3,
			1000,
			0);

		var game = setup.Game;
		game.DealerPosition = 0;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;
		await DbContext.SaveChangesAsync();

		await DatabaseSeeder.CreatePotAsync(DbContext, game, 101);

		var playerOne = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
		var playerTwo = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);
		var playerThree = setup.GamePlayers.Single(gp => gp.SeatPosition == 2);
		playerOne.TotalContributedThisHand = 50;
		playerTwo.TotalContributedThisHand = 50;
		playerThree.TotalContributedThisHand = 1;
		playerOne.IsAllIn = false;
		playerTwo.IsAllIn = false;
		playerThree.IsAllIn = false;
		playerOne.ChipStack = 950;
		playerTwo.ChipStack = 950;
		playerThree.ChipStack = 999;
		playerThree.HasFolded = true;

		var dealtAt = DateTimeOffset.UtcNow;
		DbContext.GameCards.AddRange(
			CreateHoleCard(game.Id, playerOne.Id, CardSuit.Spades, CardSymbol.Jack, 1, dealtAt),
			CreateHoleCard(game.Id, playerOne.Id, CardSuit.Clubs, CardSymbol.Ten, 2, dealtAt),
			CreateHoleCard(game.Id, playerOne.Id, CardSuit.Diamonds, CardSymbol.Deuce, 3, dealtAt),
			CreateHoleCard(game.Id, playerOne.Id, CardSuit.Hearts, CardSymbol.Three, 4, dealtAt),
			CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Hearts, CardSymbol.Jack, 1, dealtAt),
			CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Diamonds, CardSymbol.Ten, 2, dealtAt),
			CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Clubs, CardSymbol.Four, 3, dealtAt),
			CreateHoleCard(game.Id, playerTwo.Id, CardSuit.Spades, CardSymbol.Five, 4, dealtAt),
			CreateHoleCard(game.Id, playerThree.Id, CardSuit.Clubs, CardSymbol.Nine, 1, dealtAt),
			CreateHoleCard(game.Id, playerThree.Id, CardSuit.Diamonds, CardSymbol.Eight, 2, dealtAt),
			CreateHoleCard(game.Id, playerThree.Id, CardSuit.Spades, CardSymbol.Six, 3, dealtAt),
			CreateHoleCard(game.Id, playerThree.Id, CardSuit.Hearts, CardSymbol.Seven, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Diamonds, CardSymbol.Ace, 1, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Clubs, CardSymbol.King, 2, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Queen, 3, dealtAt));
		await DbContext.SaveChangesAsync();

		var result = await CreateHandler(new FakeHandSettlementService())
			.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.Payouts["Player 2"].Should().Be(51);
		result.AsT0.Payouts["Player 1"].Should().Be(50);
		result.AsT0.Payouts.Should().NotContainKey("Player 3");
		result.AsT0.Payouts.Values.Sum().Should().Be(101);

		var players = await GetFreshDbContext().GamePlayers.Include(gp => gp.Player)
			.Where(gp => gp.GameId == game.Id)
			.ToListAsync();
		players.Single(gp => gp.Player.Name == "Player 1").ChipStack.Should().Be(1000);
		players.Single(gp => gp.Player.Name == "Player 2").ChipStack.Should().Be(1001);
		players.Single(gp => gp.Player.Name == "Player 3").ChipStack.Should().Be(999);
	}

	[Fact]
	public async Task Handle_OmahaAllIn_PaysOnlyTheMatchedPot()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, PokerGameMetadataRegistry.OmahaCode, 3, 1000, 0);
		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;

		var dana = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
		var bob = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);
		var carol = setup.GamePlayers.Single(gp => gp.SeatPosition == 2);
		dana.Player.Name = "Dana";
		bob.Player.Name = "Bob";
		carol.Player.Name = "Carol";
		dana.ChipStack = 500;
		dana.TotalContributedThisHand = 500;
		dana.IsAllIn = false;
		bob.ChipStack = 0;
		bob.TotalContributedThisHand = 50;
		bob.IsAllIn = true;
		carol.ChipStack = 500;
		carol.TotalContributedThisHand = 500;
		carol.IsAllIn = false;

		await DatabaseSeeder.CreatePotAsync(DbContext, game, 1050);
		var dealtAt = DateTimeOffset.UtcNow;
		DbContext.GameCards.AddRange(
			CreateHoleCard(game.Id, dana.Id, CardSuit.Clubs, CardSymbol.Seven, 1, dealtAt),
			CreateHoleCard(game.Id, dana.Id, CardSuit.Diamonds, CardSymbol.Six, 2, dealtAt),
			CreateHoleCard(game.Id, dana.Id, CardSuit.Spades, CardSymbol.Nine, 3, dealtAt),
			CreateHoleCard(game.Id, dana.Id, CardSuit.Hearts, CardSymbol.Eight, 4, dealtAt),
			CreateHoleCard(game.Id, bob.Id, CardSuit.Hearts, CardSymbol.Jack, 1, dealtAt),
			CreateHoleCard(game.Id, bob.Id, CardSuit.Hearts, CardSymbol.Ten, 2, dealtAt),
			CreateHoleCard(game.Id, bob.Id, CardSuit.Clubs, CardSymbol.Nine, 3, dealtAt),
			CreateHoleCard(game.Id, bob.Id, CardSuit.Diamonds, CardSymbol.Eight, 4, dealtAt),
			CreateHoleCard(game.Id, carol.Id, CardSuit.Clubs, CardSymbol.Ace, 1, dealtAt),
			CreateHoleCard(game.Id, carol.Id, CardSuit.Diamonds, CardSymbol.Ace, 2, dealtAt),
			CreateHoleCard(game.Id, carol.Id, CardSuit.Clubs, CardSymbol.Four, 3, dealtAt),
			CreateHoleCard(game.Id, carol.Id, CardSuit.Diamonds, CardSymbol.Five, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Ace, 1, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.King, 2, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Queen, 3, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Clubs, CardSymbol.Deuce, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Diamonds, CardSymbol.Three, 5, dealtAt));
		await DbContext.SaveChangesAsync();

		var settlement = new FakeHandSettlementService();
		var handler = CreateHandler(settlement);

		var result = await handler.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		result.AsT0.WonByFold.Should().BeFalse();
		PositivePayouts(result.AsT0.Payouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 150,
			["Carol"] = 900
		});

		var fresh = GetFreshDbContext();
		var players = await fresh.GamePlayers.Include(gp => gp.Player)
			.Where(gp => gp.GameId == game.Id)
			.ToListAsync();
		players.Single(gp => gp.Player.Name == "Dana").ChipStack.Should().Be(500);
		players.Single(gp => gp.Player.Name == "Bob").ChipStack.Should().Be(150);
		players.Single(gp => gp.Player.Name == "Carol").ChipStack.Should().Be(1400);
		players.Single(gp => gp.Player.Name == "Dana").TotalContributedThisHand.Should().Be(500);
		players.Single(gp => gp.Player.Name == "Bob").TotalContributedThisHand.Should().Be(50);
		players.Single(gp => gp.Player.Name == "Carol").TotalContributedThisHand.Should().Be(500);

		var pots = await fresh.Pots.Include(p => p.Contributions)
			.Where(p => p.GameId == game.Id && p.HandNumber == 1)
			.OrderBy(p => p.PotOrder)
			.ToListAsync();
		pots.Should().HaveCount(2);
		pots[0].IsAwarded.Should().BeTrue();
		pots[0].PotOrder.Should().Be(0);
		pots[0].Amount.Should().Be(150);
		pots[0].PotType.Should().Be(PotType.Main);
		pots[1].IsAwarded.Should().BeTrue();
		pots[1].PotOrder.Should().Be(1);
		pots[1].Amount.Should().Be(900);
		pots[1].PotType.Should().Be(PotType.Side);
		pots[0].Contributions.Should().Contain(c => c.GamePlayerId == bob.Id && c.IsEligibleToWin);
		pots[1].Contributions.Where(c => c.GamePlayerId == bob.Id && c.IsEligibleToWin).Should().BeEmpty();

		settlement.SettledHands.Should().ContainSingle();
		PositivePayouts(settlement.SettledHands[0].Payouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 150,
			["Carol"] = 900
		});
		settlement.SettledHands[0].Payouts.Values.Should().NotContain(1050);

		var again = await handler.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);
		again.IsT0.Should().BeTrue();
		PositivePayouts(again.AsT0.Payouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 150,
			["Carol"] = 900
		});
		settlement.SettledHands.Should().ContainSingle();

		var afterSecond = await GetFreshDbContext().GamePlayers.Include(gp => gp.Player)
			.Where(gp => gp.GameId == game.Id)
			.ToListAsync();
		afterSecond.Single(gp => gp.Player.Name == "Dana").ChipStack.Should().Be(500);
		afterSecond.Single(gp => gp.Player.Name == "Bob").ChipStack.Should().Be(150);
		afterSecond.Single(gp => gp.Player.Name == "Carol").ChipStack.Should().Be(1400);
	}

	[Fact]
	public async Task Handle_HeadsUpUncalledBet_ReturnsTheUncalledChips()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, PokerGameMetadataRegistry.OmahaCode, 2, 1000, 0);
		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;

		var alice = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
		var bob = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);
		alice.Player.Name = "Alice";
		bob.Player.Name = "Bob";
		alice.ChipStack = 500;
		alice.TotalContributedThisHand = 500;
		alice.IsAllIn = false;
		bob.ChipStack = 0;
		bob.TotalContributedThisHand = 50;
		bob.IsAllIn = true;

		await DatabaseSeeder.CreatePotAsync(DbContext, game, 550);
		var dealtAt = DateTimeOffset.UtcNow;
		DbContext.GameCards.AddRange(
			CreateHoleCard(game.Id, alice.Id, CardSuit.Clubs, CardSymbol.Seven, 1, dealtAt),
			CreateHoleCard(game.Id, alice.Id, CardSuit.Diamonds, CardSymbol.Deuce, 2, dealtAt),
			CreateHoleCard(game.Id, alice.Id, CardSuit.Spades, CardSymbol.Three, 3, dealtAt),
			CreateHoleCard(game.Id, alice.Id, CardSuit.Hearts, CardSymbol.Four, 4, dealtAt),
			CreateHoleCard(game.Id, bob.Id, CardSuit.Clubs, CardSymbol.Ace, 1, dealtAt),
			CreateHoleCard(game.Id, bob.Id, CardSuit.Diamonds, CardSymbol.Ace, 2, dealtAt),
			CreateHoleCard(game.Id, bob.Id, CardSuit.Hearts, CardSymbol.Three, 3, dealtAt),
			CreateHoleCard(game.Id, bob.Id, CardSuit.Clubs, CardSymbol.Four, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.King, 1, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Diamonds, CardSymbol.Queen, 2, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Clubs, CardSymbol.Jack, 3, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Spades, CardSymbol.Nine, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Eight, 5, dealtAt));
		await DbContext.SaveChangesAsync();

		var settlement = new FakeHandSettlementService();
		var result = await CreateHandler(settlement)
			.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		PositivePayouts(result.AsT0.Payouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 100
		});

		var players = await GetFreshDbContext().GamePlayers.Include(gp => gp.Player)
			.Where(gp => gp.GameId == game.Id)
			.ToListAsync();
		var aliceAfter = players.Single(gp => gp.Player.Name == "Alice");
		var bobAfter = players.Single(gp => gp.Player.Name == "Bob");
		aliceAfter.ChipStack.Should().Be(950);
		aliceAfter.TotalContributedThisHand.Should().Be(50);
		bobAfter.ChipStack.Should().Be(100);
		(aliceAfter.ChipStack + bobAfter.ChipStack).Should().Be(1050);

		var pots = await GetFreshDbContext().Pots
			.Where(p => p.GameId == game.Id && p.HandNumber == 1 && p.IsAwarded)
			.ToListAsync();
		pots.Should().ContainSingle();
		pots[0].Amount.Should().Be(100);

		settlement.SettledHands.Should().ContainSingle();
		PositivePayouts(settlement.SettledHands[0].Payouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 100
		});
	}

	[Fact]
	public async Task Handle_PotDoesNotMatchContributions_AwardsNothing()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, PokerGameMetadataRegistry.OmahaCode, 2, 1000, 0);
		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;

		var first = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
		var second = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);
		first.ChipStack = 960;
		first.TotalContributedThisHand = 40;
		second.ChipStack = 950;
		second.TotalContributedThisHand = 50;

		await DatabaseSeeder.CreatePotAsync(DbContext, game, 100);
		var dealtAt = DateTimeOffset.UtcNow;
		DbContext.GameCards.AddRange(
			CreateHoleCard(game.Id, first.Id, CardSuit.Clubs, CardSymbol.Ace, 1, dealtAt),
			CreateHoleCard(game.Id, first.Id, CardSuit.Diamonds, CardSymbol.Ace, 2, dealtAt),
			CreateHoleCard(game.Id, first.Id, CardSuit.Spades, CardSymbol.King, 3, dealtAt),
			CreateHoleCard(game.Id, first.Id, CardSuit.Hearts, CardSymbol.Queen, 4, dealtAt),
			CreateHoleCard(game.Id, second.Id, CardSuit.Clubs, CardSymbol.King, 1, dealtAt),
			CreateHoleCard(game.Id, second.Id, CardSuit.Diamonds, CardSymbol.King, 2, dealtAt),
			CreateHoleCard(game.Id, second.Id, CardSuit.Spades, CardSymbol.Queen, 3, dealtAt),
			CreateHoleCard(game.Id, second.Id, CardSuit.Hearts, CardSymbol.Jack, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Deuce, 1, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Clubs, CardSymbol.Three, 2, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Diamonds, CardSymbol.Four, 3, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Spades, CardSymbol.Five, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Seven, 5, dealtAt));
		await DbContext.SaveChangesAsync();

		var settlement = new FakeHandSettlementService();
		var result = await CreateHandler(settlement)
			.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT1.Should().BeTrue();
		result.AsT1.Code.Should().Be(PerformShowdownErrorCode.InvalidGameState);
		result.AsT1.Message.Should().Contain("100").And.Contain("90");

		var fresh = GetFreshDbContext();
		var players = await fresh.GamePlayers.Where(gp => gp.GameId == game.Id).ToListAsync();
		players.Single(gp => gp.Id == first.Id).ChipStack.Should().Be(960);
		players.Single(gp => gp.Id == second.Id).ChipStack.Should().Be(950);
		var pot = await fresh.Pots.SingleAsync(p => p.GameId == game.Id && p.HandNumber == 1);
		pot.IsAwarded.Should().BeFalse();
		pot.Amount.Should().Be(100);
		settlement.SettledHands.Should().BeEmpty();
	}

	[Fact]
	public async Task Handle_BobBarkerAllIn_SplitsEachLayerAndRefundsUncalledChips()
	{
		var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
			DbContext, PokerGameMetadataRegistry.BobBarkerCode, 2, 1000, 0);
		var game = setup.Game;
		game.CurrentHandNumber = 1;
		game.CurrentPhase = nameof(Phases.Showdown);
		game.Status = GameStatus.InProgress;
		game.CurrentHandGameTypeCode = PokerGameMetadataRegistry.BobBarkerCode;

		var alice = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
		var bob = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);
		alice.Player.Name = "Alice";
		bob.Player.Name = "Bob";
		alice.ChipStack = 900;
		alice.TotalContributedThisHand = 100;
		alice.IsAllIn = false;
		bob.ChipStack = 0;
		bob.TotalContributedThisHand = 40;
		bob.IsAllIn = true;
		BobBarkerVariantState.SetSelectedShowcaseDealOrder(alice, 5);
		BobBarkerVariantState.SetSelectedShowcaseDealOrder(bob, 5);

		await DatabaseSeeder.CreatePotAsync(DbContext, game, 140);
		var dealtAt = DateTimeOffset.UtcNow;
		DbContext.GameCards.AddRange(
			CreateHandCard(game.Id, alice.Id, CardSuit.Clubs, CardSymbol.Seven, 1, dealtAt),
			CreateHandCard(game.Id, alice.Id, CardSuit.Diamonds, CardSymbol.Six, 2, dealtAt),
			CreateHandCard(game.Id, alice.Id, CardSuit.Spades, CardSymbol.Five, 3, dealtAt),
			CreateHandCard(game.Id, alice.Id, CardSuit.Hearts, CardSymbol.Four, 4, dealtAt),
			CreateHandCard(game.Id, alice.Id, CardSuit.Clubs, CardSymbol.Three, 5, dealtAt),
			CreateHandCard(game.Id, bob.Id, CardSuit.Hearts, CardSymbol.Jack, 1, dealtAt),
			CreateHandCard(game.Id, bob.Id, CardSuit.Hearts, CardSymbol.Ten, 2, dealtAt),
			CreateHandCard(game.Id, bob.Id, CardSuit.Clubs, CardSymbol.Nine, 3, dealtAt),
			CreateHandCard(game.Id, bob.Id, CardSuit.Diamonds, CardSymbol.Eight, 4, dealtAt),
			CreateHandCard(game.Id, bob.Id, CardSuit.Clubs, CardSymbol.King, 5, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Clubs, CardSymbol.Four, 0, dealtAt, false),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Ace, 1, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.King, 2, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Hearts, CardSymbol.Queen, 3, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Clubs, CardSymbol.Deuce, 4, dealtAt),
			CreateCommunityCard(game.Id, CardSuit.Diamonds, CardSymbol.Three, 5, dealtAt));
		await DbContext.SaveChangesAsync();

		var settlement = new FakeHandSettlementService();
		var result = await CreateHandler(settlement)
			.Handle(new PerformShowdownCommand(game.Id), CancellationToken.None);

		result.IsT0.Should().BeTrue();
		PositivePayouts(result.AsT0.Payouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 40,
			["Alice"] = 40
		});

		var players = await GetFreshDbContext().GamePlayers.Include(gp => gp.Player)
			.Where(gp => gp.GameId == game.Id)
			.ToListAsync();
		var aliceAfter = players.Single(gp => gp.Player.Name == "Alice");
		aliceAfter.ChipStack.Should().Be(1000);
		aliceAfter.TotalContributedThisHand.Should().Be(40);
		players.Single(gp => gp.Player.Name == "Bob").ChipStack.Should().Be(40);

		var pots = await GetFreshDbContext().Pots
			.Where(p => p.GameId == game.Id && p.HandNumber == 1)
			.ToListAsync();
		pots.Should().ContainSingle();
		pots[0].IsAwarded.Should().BeTrue();
		pots[0].Amount.Should().Be(80);

		PositivePayouts(settlement.SettledHands.Single().Payouts).Should().BeEquivalentTo(new Dictionary<string, int>
		{
			["Bob"] = 40,
			["Alice"] = 40
		});
	}

	private PerformShowdownCommandHandler CreateHandler(FakeHandSettlementService settlement)
	{
		return new PerformShowdownCommandHandler(
			DbContext,
			FlowHandlerFactory,
			new HandEvaluatorFactory(),
			new FakeHandHistoryRecorder(),
			settlement,
			NullLogger<PerformShowdownCommandHandler>.Instance);
	}

	private static Dictionary<string, int> PositivePayouts(Dictionary<string, int> payouts)
	{
		return payouts.Where(payout => payout.Value > 0).ToDictionary(payout => payout.Key, payout => payout.Value);
	}

	private static GameCard CreateHoleCard(
		Guid gameId,
		Guid gamePlayerId,
		CardSuit suit,
		CardSymbol symbol,
		int dealOrder,
		DateTimeOffset dealtAt)
	{
		return new GameCard
		{
			GameId = gameId,
			GamePlayerId = gamePlayerId,
			HandNumber = 1,
			Suit = suit,
			Symbol = symbol,
			Location = CardLocation.Hole,
			DealOrder = dealOrder,
			DealtAtPhase = nameof(Phases.PreFlop),
			IsVisible = false,
			DealtAt = dealtAt
		};
	}

	private static GameCard CreateHandCard(
		Guid gameId,
		Guid gamePlayerId,
		CardSuit suit,
		CardSymbol symbol,
		int dealOrder,
		DateTimeOffset dealtAt)
	{
		var card = CreateHoleCard(gameId, gamePlayerId, suit, symbol, dealOrder, dealtAt);
		card.Location = CardLocation.Hand;
		return card;
	}

	private static GameCard CreateCommunityCard(
		Guid gameId,
		CardSuit suit,
		CardSymbol symbol,
		int dealOrder,
		DateTimeOffset dealtAt,
		bool isVisible = true)
	{
		return new GameCard
		{
			GameId = gameId,
			GamePlayerId = null,
			HandNumber = 1,
			Suit = suit,
			Symbol = symbol,
			Location = CardLocation.Community,
			DealOrder = dealOrder,
			DealtAtPhase = nameof(Phases.Flop),
			IsVisible = isVisible,
			IsDiscarded = false,
			DealtAt = dealtAt
		};
	}
}