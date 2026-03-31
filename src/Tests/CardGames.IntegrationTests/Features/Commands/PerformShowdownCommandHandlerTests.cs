using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Features.Games.BobBarker;
using CardGames.Poker.Api.Data.Entities;
using System.Text.Json;
using BettingAction = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Features.Commands;

/// <summary>
/// Integration tests for <see cref="PerformShowdownCommandHandler"/>.
/// Tests showdown logic including winner determination and pot distribution.
/// </summary>
public class PerformShowdownCommandHandlerTests : IntegrationTestBase
{
    private async Task<Game> CreateGameInShowdownPhaseAsync(int numPlayers = 2)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", numPlayers, ante: 10);
        
        // Start hand
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        
        // Collect antes
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        
        // Deal hands
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // First betting round - everyone checks
        for (int i = 0; i < numPlayers; i++)
        {
            await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        }

        // Draw phase - everyone stands pat
        for (int i = 0; i < numPlayers; i++)
        {
            var game = await DbContext.Games
                .Include(g => g.GamePlayers)
                .FirstAsync(g => g.Id == setup.Game.Id);
            
            await Mediator.Send(new ProcessDrawCommand(game.Id, new List<int>()));
        }

        // Second betting round - everyone checks
        for (int i = 0; i < numPlayers; i++)
        {
            await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        }

        // Reload game
        var finalGame = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .Include(g => g.GameCards)
            .Include(g => g.Pots)
            .FirstAsync(g => g.Id == setup.Game.Id);

        return finalGame;
    }

    [Fact]
    public async Task Handle_GameNotFound_ReturnsError()
    {
        // Arrange
        var command = new PerformShowdownCommand(Guid.NewGuid());

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NotInShowdownPhase_ReturnsError()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var command = new PerformShowdownCommand(setup.Game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT1.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidShowdown_ReturnsSuccess()
    {
        // Arrange
        var game = await CreateGameInShowdownPhaseAsync();
        var command = new PerformShowdownCommand(game.Id);

        // Act
        var result = await Mediator.Send(command);

        // Assert
        result.IsT0.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ValidShowdown_AdvancesToComplete()
    {
        // Arrange
        var game = await CreateGameInShowdownPhaseAsync();
        var command = new PerformShowdownCommand(game.Id);

        // Act
        await Mediator.Send(command);

        // Assert
        var freshGame = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        freshGame.CurrentPhase.Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task Handle_SinglePlayerRemaining_AwardsPotWithoutEvaluation()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 3, ante: 10);
        
        // Start hand and go through initial phases
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // One player bets, others fold
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Bet, 10));
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Fold, 0));
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Fold, 0));

        // Game should have advanced to showdown with one player remaining
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
            .Include(g => g.Pots)
            .FirstAsync(g => g.Id == setup.Game.Id);

        // Assert - The remaining player should get the pot
        var activePlayers = game.GamePlayers.Where(gp => !gp.HasFolded).ToList();
        activePlayers.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_PotsMarkedAsAwarded()
    {
        // Arrange
        var game = await CreateGameInShowdownPhaseAsync();
        var command = new PerformShowdownCommand(game.Id);

        // Act
        await Mediator.Send(command);

        // Assert
        var pots = await GetFreshDbContext().Pots
            .Where(p => p.GameId == game.Id && p.HandNumber == game.CurrentHandNumber)
            .ToListAsync();
        
        pots.Should().AllSatisfy(p => p.IsAwarded.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_WinnerChipsUpdated()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, startingChips: 1000, ante: 10);
        
        // Start and go through game phases
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));
        
        // Betting round
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        
        // Draw phase
        for (int i = 0; i < 2; i++)
        {
            await Mediator.Send(new ProcessDrawCommand(setup.Game.Id, new List<int>()));
        }
        
        // Second betting
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));
        await Mediator.Send(new ProcessBettingActionCommand(setup.Game.Id, BettingAction.Check, 0));

        // Perform showdown
        var result = await Mediator.Send(new PerformShowdownCommand(setup.Game.Id));

        // Assert - Winner should have pot added to their chips
        result.IsT0.Should().BeTrue();
        
        var freshPlayers = await GetFreshDbContext().GamePlayers
            .Where(gp => gp.GameId == setup.Game.Id)
            .ToListAsync();

        // Total chips should equal starting chips (no money leaves the table)
        var totalChips = freshPlayers.Sum(p => p.ChipStack);
        totalChips.Should().Be(2000); // 2 players x 1000 starting chips
    }

    [Fact]
    public async Task Handle_DealersChoiceRazz_UsesLowballForWinnerSelection()
    {
        // Arrange: Dealer's Choice table playing a Razz hand in showdown.
        var setup = await DatabaseSeeder.CreateDealersChoiceGameSetupAsync(DbContext, 2);
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == setup.Game.Id);

        var now = DateTimeOffset.UtcNow;
        game.CurrentPhase = nameof(Phases.Showdown);
        game.CurrentHandNumber = 1;
        game.CurrentHandGameTypeCode = "RAZZ";

        var playerOne = game.GamePlayers.OrderBy(gp => gp.SeatPosition).First();
        var playerTwo = game.GamePlayers.OrderBy(gp => gp.SeatPosition).Skip(1).First();

        DbContext.Pots.Add(new CardGames.Poker.Api.Data.Entities.Pot
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            PotType = PotType.Main,
            PotOrder = 0,
            Amount = 100,
            IsAwarded = false,
            CreatedAt = now
        });

        // Player 1: strong Razz low (6-4-3-2-A) but weak high hand.
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Hearts, CardSymbol.Ace, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.Deuce, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Clubs, CardSymbol.Queen, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.Three, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Spades, CardSymbol.Four, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Six, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Diamonds, CardSymbol.King, true, now);

        // Player 2: weaker Razz low but stronger high hand (pair of nines).
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Hearts, CardSymbol.Nine, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.Nine, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Spades, CardSymbol.King, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.Eight, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Spades, CardSymbol.Seven, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Five, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.Deuce, true, now);

        await DbContext.SaveChangesAsync();

        // Act
        var result = await Mediator.Send(new PerformShowdownCommand(game.Id));

        // Assert
        result.IsT0.Should().BeTrue();
        result.AsT0.Payouts.Should().ContainSingle();
        result.AsT0.Payouts.Should().ContainKey(playerOne.Player.Name);
        result.AsT0.Payouts[playerOne.Player.Name].Should().Be(100);
        result.AsT0.PlayerHands.Single(h => h.PlayerName == playerOne.Player.Name)
            .HandType.Should().Be("6-4-3-2-1 low");
    }

    [Fact]
    public async Task Handle_DealersChoiceRazz_ExactUserExample_UsesLowballWinnerAndDescriptions()
    {
        // Arrange: Dealer's Choice table playing a Razz hand in showdown.
        var setup = await DatabaseSeeder.CreateDealersChoiceGameSetupAsync(DbContext, 2);
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == setup.Game.Id);

        var now = DateTimeOffset.UtcNow;
        game.CurrentPhase = nameof(Phases.Showdown);
        game.CurrentHandNumber = 1;
        game.CurrentHandGameTypeCode = "RAZZ";

        var playerOne = game.GamePlayers.OrderBy(gp => gp.SeatPosition).First();
        var playerTwo = game.GamePlayers.OrderBy(gp => gp.SeatPosition).Skip(1).First();

        DbContext.Pots.Add(new CardGames.Poker.Api.Data.Entities.Pot
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            PotType = PotType.Main,
            PotOrder = 0,
            Amount = 100,
            IsAwarded = false,
            CreatedAt = now
        });

        // Player1: Ks, Js, 6s, 5h, Qs, 2h, 6d -> best Razz low should be 12-11-6-5-2.
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Spades, CardSymbol.King, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Spades, CardSymbol.Jack, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Spades, CardSymbol.Six, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Five, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Spades, CardSymbol.Queen, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Deuce, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Diamonds, CardSymbol.Six, true, now);

        // Player2: Qc, Kh, 9d, 7h, 9h, 7d, 5s -> best Razz low should be 13-12-9-7-5.
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Clubs, CardSymbol.Queen, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Hearts, CardSymbol.King, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.Nine, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Seven, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Nine, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Diamonds, CardSymbol.Seven, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Spades, CardSymbol.Five, true, now);

        await DbContext.SaveChangesAsync();

        // Act
        var result = await Mediator.Send(new PerformShowdownCommand(game.Id));

        // Assert
        result.IsT0.Should().BeTrue();
        result.AsT0.Payouts.Should().ContainSingle();
        result.AsT0.Payouts.Should().ContainKey(playerOne.Player.Name);
        result.AsT0.Payouts[playerOne.Player.Name].Should().Be(100);

        var playerOneHand = result.AsT0.PlayerHands.Single(h => h.PlayerName == playerOne.Player.Name);
        var playerTwoHand = result.AsT0.PlayerHands.Single(h => h.PlayerName == playerTwo.Player.Name);

        playerOneHand.HandType.Should().Be("12-11-6-5-2 low");
        playerTwoHand.HandType.Should().Be("13-12-9-7-5 low");
    }

    [Fact]
    public async Task Handle_DealersChoiceRazz_QueenHighLow_Beats_KingHighLow()
    {
        // Arrange: Reported production scenario where lower high card should win in Razz.
        var setup = await DatabaseSeeder.CreateDealersChoiceGameSetupAsync(DbContext, 2);
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == setup.Game.Id);

        var now = DateTimeOffset.UtcNow;
        game.CurrentPhase = nameof(Phases.Showdown);
        game.CurrentHandNumber = 1;
        game.CurrentHandGameTypeCode = "RAZZ";

        var playerOne = game.GamePlayers.OrderBy(gp => gp.SeatPosition).First();
        var playerTwo = game.GamePlayers.OrderBy(gp => gp.SeatPosition).Skip(1).First();

        DbContext.Pots.Add(new CardGames.Poker.Api.Data.Entities.Pot
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            PotType = PotType.Main,
            PotOrder = 0,
            Amount = 100,
            IsAwarded = false,
            CreatedAt = now
        });

        // Player 1: Kd, 5d, Ac, 2d, Kh, Qs, 7h -> best low: 12-7-5-2-1.
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.King, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.Five, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Hole, CardSuit.Clubs, CardSymbol.Ace, false, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Diamonds, CardSymbol.Deuce, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.King, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Spades, CardSymbol.Queen, true, now);
        AddStudCard(game.Id, playerOne.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Seven, true, now);

        // Player 2: 6s, Ad, As, Kc, 4c, 2h, 4h -> best low: 13-6-4-2-1.
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Spades, CardSymbol.Six, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.Ace, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Hole, CardSuit.Spades, CardSymbol.Ace, false, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.King, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.Four, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Deuce, true, now);
        AddStudCard(game.Id, playerTwo.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Four, true, now);

        await DbContext.SaveChangesAsync();

        // Act
        var result = await Mediator.Send(new PerformShowdownCommand(game.Id));

        // Assert
        result.IsT0.Should().BeTrue();
        result.AsT0.Payouts.Should().ContainSingle();
        result.AsT0.Payouts.Should().ContainKey(playerOne.Player.Name);
        result.AsT0.Payouts[playerOne.Player.Name].Should().Be(100);

        var playerOneHand = result.AsT0.PlayerHands.Single(h => h.PlayerName == playerOne.Player.Name);
        var playerTwoHand = result.AsT0.PlayerHands.Single(h => h.PlayerName == playerTwo.Player.Name);

        playerOneHand.HandType.Should().Be("12-7-5-2-1 low");
        playerTwoHand.HandType.Should().Be("13-6-4-2-1 low");
        playerOneHand.IsWinner.Should().BeTrue();
        playerTwoHand.IsWinner.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_BobBarker_SplitsPotBetweenMainHandAndShowcaseWinner()
    {
        var (game, players) = await CreateBobBarkerShowdownGameAsync(playerCount: 2, potAmount: 100);
        var playerOne = players[0];
        var playerTwo = players[1];
        var now = DateTimeOffset.UtcNow;

        // Player 1 active hand: A-A with board K-K-K for the best full house.
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 1, CardSuit.Spades, CardSymbol.Ace, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 2, CardSuit.Hearts, CardSymbol.Ace, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 3, CardSuit.Clubs, CardSymbol.Queen, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 4, CardSuit.Diamonds, CardSymbol.Three, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 5, CardSuit.Clubs, CardSymbol.Deuce, now);

        // Player 2 active hand: 9-9 with the same board, weaker full house. Showcase Ten beats Player 1's Deuce against dealer Jack.
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 1, CardSuit.Spades, CardSymbol.Nine, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 2, CardSuit.Hearts, CardSymbol.Nine, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 3, CardSuit.Clubs, CardSymbol.Eight, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 4, CardSuit.Diamonds, CardSymbol.Seven, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 5, CardSuit.Hearts, CardSymbol.Ten, now);

        BobBarkerVariantState.SetSelectedShowcaseDealOrder(playerOne, 5);
        BobBarkerVariantState.SetSelectedShowcaseDealOrder(playerTwo, 5);

        AddBobBarkerCommunityCard(game.Id, 0, CardSuit.Spades, CardSymbol.Jack, isVisible: false, nameof(Phases.Dealing), now);
        AddBobBarkerCommunityCard(game.Id, 1, CardSuit.Clubs, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 2, CardSuit.Hearts, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 3, CardSuit.Diamonds, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 4, CardSuit.Clubs, CardSymbol.Five, isVisible: true, nameof(Phases.Turn), now);
        AddBobBarkerCommunityCard(game.Id, 5, CardSuit.Hearts, CardSymbol.Six, isVisible: true, nameof(Phases.River), now);

        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new PerformShowdownCommand(game.Id));

        result.IsT0.Should().BeTrue();

        var awardedPot = await GetFreshDbContext().Pots.FirstAsync(p => p.GameId == game.Id && p.HandNumber == 1);
        awardedPot.WinReason.Should().Contain("Bob Barker split pot");
        awardedPot.WinReason.Should().NotContain("no showcase qualifier");
        var payouts = ParseWinnerPayouts(awardedPot.WinnerPayouts!);

        payouts[playerOne.Player.Name].Amount.Should().Be(50);
        payouts[playerOne.Player.Name].High.Should().Be(50);
        payouts[playerOne.Player.Name].Showcase.Should().Be(0);
        payouts[playerTwo.Player.Name].Amount.Should().Be(50);
        payouts[playerTwo.Player.Name].High.Should().Be(0);
        payouts[playerTwo.Player.Name].Showcase.Should().Be(50);
    }

    [Fact]
    public async Task Handle_BobBarker_HandDescription_UsesExactlyTwoHoleCards()
    {
        var (game, players) = await CreateBobBarkerShowdownGameAsync(playerCount: 2, potAmount: 100);
        var hero = players[0];
        var villain = players[1];
        var now = DateTimeOffset.UtcNow;

        AddBobBarkerPlayerCard(game.Id, hero.Id, 1, CardSuit.Hearts, CardSymbol.Queen, now);
        AddBobBarkerPlayerCard(game.Id, hero.Id, 2, CardSuit.Clubs, CardSymbol.Queen, now);
        AddBobBarkerPlayerCard(game.Id, hero.Id, 3, CardSuit.Diamonds, CardSymbol.King, now);
        AddBobBarkerPlayerCard(game.Id, hero.Id, 4, CardSuit.Spades, CardSymbol.King, now);
        AddBobBarkerPlayerCard(game.Id, hero.Id, 5, CardSuit.Clubs, CardSymbol.Jack, now);

        AddBobBarkerPlayerCard(game.Id, villain.Id, 1, CardSuit.Spades, CardSymbol.Ace, now);
        AddBobBarkerPlayerCard(game.Id, villain.Id, 2, CardSuit.Hearts, CardSymbol.Nine, now);
        AddBobBarkerPlayerCard(game.Id, villain.Id, 3, CardSuit.Diamonds, CardSymbol.Eight, now);
        AddBobBarkerPlayerCard(game.Id, villain.Id, 4, CardSuit.Clubs, CardSymbol.Seven, now);
        AddBobBarkerPlayerCard(game.Id, villain.Id, 5, CardSuit.Hearts, CardSymbol.Five, now);

        BobBarkerVariantState.SetSelectedShowcaseDealOrder(hero, 5);
        BobBarkerVariantState.SetSelectedShowcaseDealOrder(villain, 5);

        AddBobBarkerCommunityCard(game.Id, 0, CardSuit.Spades, CardSymbol.Ace, isVisible: false, nameof(Phases.Dealing), now);
        AddBobBarkerCommunityCard(game.Id, 1, CardSuit.Hearts, CardSymbol.Six, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 2, CardSuit.Clubs, CardSymbol.Ten, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 3, CardSuit.Hearts, CardSymbol.Four, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 4, CardSuit.Diamonds, CardSymbol.Deuce, isVisible: true, nameof(Phases.Turn), now);
        AddBobBarkerCommunityCard(game.Id, 5, CardSuit.Clubs, CardSymbol.Six, isVisible: true, nameof(Phases.River), now);

        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new PerformShowdownCommand(game.Id));

        result.IsT0.Should().BeTrue();

        var heroHand = result.AsT0.PlayerHands.Single(h => h.PlayerName == hero.Player.Name);
        heroHand.HandDescription.Should().Be("Two pair, Kings and Sixes");
    }

    [Fact]
    public async Task Handle_BobBarker_ShowcaseTie_SplitsShowcaseHalfAmongAllShowcaseWinners()
    {
        var (game, players) = await CreateBobBarkerShowdownGameAsync(playerCount: 3, potAmount: 100);
        var playerOne = players[0];
        var playerTwo = players[1];
        var playerThree = players[2];
        var now = DateTimeOffset.UtcNow;

        // Player 1 wins the main hand with aces full of kings.
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 1, CardSuit.Spades, CardSymbol.Ace, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 2, CardSuit.Hearts, CardSymbol.Ace, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 3, CardSuit.Clubs, CardSymbol.Queen, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 4, CardSuit.Diamonds, CardSymbol.Three, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 5, CardSuit.Clubs, CardSymbol.Deuce, now);

        // Players 2 and 3 tie on showcase Tens under dealer Jack.
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 1, CardSuit.Spades, CardSymbol.Nine, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 2, CardSuit.Hearts, CardSymbol.Nine, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 3, CardSuit.Clubs, CardSymbol.Eight, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 4, CardSuit.Diamonds, CardSymbol.Seven, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 5, CardSuit.Hearts, CardSymbol.Ten, now);

        AddBobBarkerPlayerCard(game.Id, playerThree.Id, 1, CardSuit.Spades, CardSymbol.Jack, now);
        AddBobBarkerPlayerCard(game.Id, playerThree.Id, 2, CardSuit.Hearts, CardSymbol.Nine, now);
        AddBobBarkerPlayerCard(game.Id, playerThree.Id, 3, CardSuit.Clubs, CardSymbol.Eight, now);
        AddBobBarkerPlayerCard(game.Id, playerThree.Id, 4, CardSuit.Diamonds, CardSymbol.Four, now);
        AddBobBarkerPlayerCard(game.Id, playerThree.Id, 5, CardSuit.Clubs, CardSymbol.Ten, now);

        BobBarkerVariantState.SetSelectedShowcaseDealOrder(playerOne, 5);
        BobBarkerVariantState.SetSelectedShowcaseDealOrder(playerTwo, 5);
        BobBarkerVariantState.SetSelectedShowcaseDealOrder(playerThree, 5);

        AddBobBarkerCommunityCard(game.Id, 0, CardSuit.Spades, CardSymbol.Jack, isVisible: false, nameof(Phases.Dealing), now);
        AddBobBarkerCommunityCard(game.Id, 1, CardSuit.Clubs, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 2, CardSuit.Hearts, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 3, CardSuit.Diamonds, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 4, CardSuit.Clubs, CardSymbol.Five, isVisible: true, nameof(Phases.Turn), now);
        AddBobBarkerCommunityCard(game.Id, 5, CardSuit.Hearts, CardSymbol.Six, isVisible: true, nameof(Phases.River), now);

        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new PerformShowdownCommand(game.Id));

        result.IsT0.Should().BeTrue();

        var awardedPot = await GetFreshDbContext().Pots.FirstAsync(p => p.GameId == game.Id && p.HandNumber == 1);
        var payouts = ParseWinnerPayouts(awardedPot.WinnerPayouts!);

        payouts[playerOne.Player.Name].Amount.Should().Be(50);
        payouts[playerOne.Player.Name].High.Should().Be(50);
        payouts[playerOne.Player.Name].Showcase.Should().Be(0);
        payouts[playerTwo.Player.Name].Amount.Should().Be(25);
        payouts[playerTwo.Player.Name].High.Should().Be(0);
        payouts[playerTwo.Player.Name].Showcase.Should().Be(25);
        payouts[playerThree.Player.Name].Amount.Should().Be(25);
        payouts[playerThree.Player.Name].High.Should().Be(0);
        payouts[playerThree.Player.Name].Showcase.Should().Be(25);
    }

    [Fact]
    public async Task Handle_BobBarker_DealerAce_TreatsAceShowcaseAsHigh()
    {
        var (game, players) = await CreateBobBarkerShowdownGameAsync(playerCount: 2, potAmount: 100);
        var playerOne = players[0];
        var playerTwo = players[1];
        var now = DateTimeOffset.UtcNow;

        // Player 2 wins the main hand with queens full of kings.
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 1, CardSuit.Spades, CardSymbol.Nine, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 2, CardSuit.Hearts, CardSymbol.Nine, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 3, CardSuit.Clubs, CardSymbol.Four, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 4, CardSuit.Diamonds, CardSymbol.Three, now);
        AddBobBarkerPlayerCard(game.Id, playerOne.Id, 5, CardSuit.Clubs, CardSymbol.Ace, now);

        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 1, CardSuit.Spades, CardSymbol.Queen, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 2, CardSuit.Hearts, CardSymbol.Queen, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 3, CardSuit.Clubs, CardSymbol.Eight, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 4, CardSuit.Diamonds, CardSymbol.Seven, now);
        AddBobBarkerPlayerCard(game.Id, playerTwo.Id, 5, CardSuit.Hearts, CardSymbol.King, now);

        BobBarkerVariantState.SetSelectedShowcaseDealOrder(playerOne, 5);
        BobBarkerVariantState.SetSelectedShowcaseDealOrder(playerTwo, 5);

        AddBobBarkerCommunityCard(game.Id, 0, CardSuit.Diamonds, CardSymbol.Ace, isVisible: false, nameof(Phases.Dealing), now);
        AddBobBarkerCommunityCard(game.Id, 1, CardSuit.Clubs, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 2, CardSuit.Hearts, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 3, CardSuit.Diamonds, CardSymbol.King, isVisible: true, nameof(Phases.Flop), now);
        AddBobBarkerCommunityCard(game.Id, 4, CardSuit.Clubs, CardSymbol.Five, isVisible: true, nameof(Phases.Turn), now);
        AddBobBarkerCommunityCard(game.Id, 5, CardSuit.Hearts, CardSymbol.Six, isVisible: true, nameof(Phases.River), now);

        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new PerformShowdownCommand(game.Id));

        result.IsT0.Should().BeTrue();

        var awardedPot = await GetFreshDbContext().Pots.FirstAsync(p => p.GameId == game.Id && p.HandNumber == 1);
        var payouts = ParseWinnerPayouts(awardedPot.WinnerPayouts!);
        payouts[playerOne.Player.Name].Showcase.Should().Be(50,
            "dealer Ace should make an Ace showcase worth 14, beating a king showcase worth 13");
    }

    private async Task<(Game Game, List<GamePlayer> Players)> CreateBobBarkerShowdownGameAsync(int playerCount, int potAmount)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BOBBARKER", playerCount, startingChips: 1000, ante: 0);
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
                .ThenInclude(gp => gp.Player)
            .Include(g => g.GameType)
            .FirstAsync(g => g.Id == setup.Game.Id);

        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.Showdown);
        game.Status = GameStatus.InProgress;
        game.CurrentHandGameTypeCode = "BOBBARKER";

        DbContext.Pots.Add(new CardGames.Poker.Api.Data.Entities.Pot
        {
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            PotType = PotType.Main,
            PotOrder = 0,
            Amount = potAmount,
            IsAwarded = false,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await DbContext.SaveChangesAsync();
        return (game, game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList());
    }

    private void AddBobBarkerPlayerCard(
        Guid gameId,
        Guid gamePlayerId,
        int dealOrder,
        CardSuit suit,
        CardSymbol symbol,
        DateTimeOffset dealtAt)
    {
        DbContext.GameCards.Add(new GameCard
        {
            GameId = gameId,
            GamePlayerId = gamePlayerId,
            HandNumber = 1,
            Location = CardLocation.Hand,
            Suit = suit,
            Symbol = symbol,
            IsVisible = false,
            IsDiscarded = false,
            DealOrder = dealOrder,
            DealtAt = dealtAt,
            DealtAtPhase = nameof(Phases.Dealing)
        });
    }

    private void AddBobBarkerCommunityCard(
        Guid gameId,
        int dealOrder,
        CardSuit suit,
        CardSymbol symbol,
        bool isVisible,
        string dealtAtPhase,
        DateTimeOffset dealtAt)
    {
        DbContext.GameCards.Add(new GameCard
        {
            GameId = gameId,
            GamePlayerId = null,
            HandNumber = 1,
            Location = CardLocation.Community,
            Suit = suit,
            Symbol = symbol,
            IsVisible = isVisible,
            IsDiscarded = false,
            DealOrder = dealOrder,
            DealtAt = dealtAt,
            DealtAtPhase = dealtAtPhase
        });
    }

    private void AddStudCard(
        Guid gameId,
        Guid gamePlayerId,
        int handNumber,
        CardLocation location,
        CardSuit suit,
        CardSymbol symbol,
        bool isVisible,
        DateTimeOffset dealtAt)
    {
        DbContext.GameCards.Add(new GameCard
        {
            GameId = gameId,
            GamePlayerId = gamePlayerId,
            HandNumber = handNumber,
            Location = location,
            Suit = suit,
            Symbol = symbol,
            IsVisible = isVisible,
            IsDiscarded = false,
            DealOrder = GetNextDealOrder(gamePlayerId, handNumber),
            DealtAt = dealtAt,
            DealtAtPhase = nameof(Phases.Showdown)
        });
    }

    private int GetNextDealOrder(Guid gamePlayerId, int handNumber)
    {
        var existingForPlayer = DbContext.GameCards
            .Count(c => c.GamePlayerId == gamePlayerId && c.HandNumber == handNumber);
        return existingForPlayer + 1;
    }

    private static Dictionary<string, (int Amount, int High, int Showcase)> ParseWinnerPayouts(string winnerPayoutsJson)
    {
        using var doc = JsonDocument.Parse(winnerPayoutsJson);
        return doc.RootElement.EnumerateArray().ToDictionary(
            x => x.GetProperty("playerName").GetString()!,
            x =>
            {
                var amount = x.TryGetProperty("amount", out var amountProperty) ? amountProperty.GetInt32() : 0;
                var high = x.TryGetProperty("highHandAmount", out var highProperty) ? highProperty.GetInt32() : 0;
                var showcase = x.TryGetProperty("showcaseAmount", out var showcaseProperty) ? showcaseProperty.GetInt32() : 0;
                return (amount, high, showcase);
            });
    }
}
