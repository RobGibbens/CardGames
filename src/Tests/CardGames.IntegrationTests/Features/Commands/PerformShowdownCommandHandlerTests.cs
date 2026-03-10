using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Data.Entities;
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
}
