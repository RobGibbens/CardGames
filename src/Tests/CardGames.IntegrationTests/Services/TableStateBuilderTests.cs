using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.CollectAntes;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.DealHands;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.IntegrationTests.Services;

/// <summary>
/// Integration tests for <see cref="TableStateBuilder"/>.
/// Tests building public and private state snapshots for games.
/// </summary>
public class TableStateBuilderTests : IntegrationTestBase
{
    private ITableStateBuilder TableStateBuilder => Scope.ServiceProvider.GetRequiredService<ITableStateBuilder>();

    [Fact]
    public async Task BuildPublicStateAsync_GameNotFound_ReturnsNull()
    {
        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildPublicStateAsync_ExistingGame_ReturnsState()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.GameId.Should().Be(setup.Game.Id);
    }

    [Fact]
    public async Task BuildPublicStateAsync_IncludesSeats()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Seats.Should().HaveCount(4);
    }

    [Fact]
    public async Task BuildPublicStateAsync_IncludesPotTotal()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4, ante: 10);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.TotalPot.Should().Be(40); // 4 players x 10 ante
    }

    [Fact]
    public async Task BuildPublicStateAsync_HoldTheBaseball_AfterStartHand_ExposesBlindPotState()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDTHEBASEBALL", 4, ante: 0);
        setup.Game.SmallBlind = 5;
        setup.Game.BigBlind = 10;
        setup.Game.DealerPosition = 0;
        await DbContext.SaveChangesAsync();

        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.GameTypeCode.Should().Be("HOLDTHEBASEBALL");
        result.CurrentPhase.Should().Be(nameof(Phases.PreFlop));
        result.TotalPot.Should().Be(15);
        result.DealerSeatIndex.Should().Be(0);
        result.Seats.Should().HaveCount(4);

        var orderedSeats = result.Seats.OrderBy(s => s.SeatIndex).ToList();
        orderedSeats[1].CurrentBet.Should().Be(5, "small blind seat should reflect posted blind in table state");
        orderedSeats[2].CurrentBet.Should().Be(10, "big blind seat should reflect posted blind in table state");
    }

    [Fact]
    public async Task BuildPublicStateAsync_AfterDeal_CardsHidden()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        // In public state, cards should be face down (not showing values)
        result!.Seats.Should().AllSatisfy(seat =>
        {
            seat.Cards.Should().NotBeNull();
        });
    }

    [Fact]
    public async Task BuildPublicStateAsync_IncludesCurrentPhase()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentPhase.Should().Be(nameof(Phases.CollectingAntes));
    }

    [Fact]
    public async Task BuildPrivateStateAsync_GameNotFound_ReturnsNull()
    {
        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(Guid.NewGuid(), "someuser");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task BuildPrivateStateAsync_ExistingGame_ReturnsState()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var playerEmail = setup.Players[0].Email;

        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, playerEmail!);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task BuildPrivateStateAsync_AfterDeal_ShowsPlayerCards()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 4);
        var playerEmail = setup.Players[0].Email;
        
        await Mediator.Send(new StartHandCommand(setup.Game.Id));
        await Mediator.Send(new CollectAntesCommand(setup.Game.Id));
        await Mediator.Send(new DealHandsCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, playerEmail!);

        // Assert
        result.Should().NotBeNull();
        result!.Hand.Should().NotBeEmpty();
        result.Hand.Should().HaveCount(5);
    }

    [Fact]
    public async Task BuildPrivateStateAsync_HoldTheBaseball_PreflopAndFlop_ApplyWildCardEvaluation()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "HOLDTHEBASEBALL", 2);
        var game = setup.Game;
        var hero = setup.GamePlayers[0];
        var heroEmail = setup.Players[0].Email!;

        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.PreFlop);
        game.Status = GameStatus.InProgress;

        DbContext.GameCards.AddRange(
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = hero.Id,
                HandNumber = 1,
                Location = CardLocation.Hole,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.Nine,
                DealOrder = 1,
                DealtAtPhase = nameof(Phases.PreFlop),
                IsVisible = false
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = hero.Id,
                HandNumber = 1,
                Location = CardLocation.Hole,
                Suit = CardSuit.Spades,
                Symbol = CardSymbol.Queen,
                DealOrder = 2,
                DealtAtPhase = nameof(Phases.PreFlop),
                IsVisible = false
            });

        await DbContext.SaveChangesAsync();

        // Act: preflop with 9h (wild) + Qs
        var preflopState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, heroEmail);

        // Assert
        preflopState.Should().NotBeNull();
        preflopState!.HandEvaluationDescription.Should().Be("Pair of Queens");

        // Arrange flop: 5c, 7h, Qh
        DbContext.GameCards.AddRange(
            new GameCard
            {
                GameId = game.Id,
                HandNumber = 1,
                Location = CardLocation.Community,
                Suit = CardSuit.Clubs,
                Symbol = CardSymbol.Five,
                DealOrder = 3,
                DealtAtPhase = nameof(Phases.Flop),
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                HandNumber = 1,
                Location = CardLocation.Community,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.Seven,
                DealOrder = 4,
                DealtAtPhase = nameof(Phases.Flop),
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                HandNumber = 1,
                Location = CardLocation.Community,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.Queen,
                DealOrder = 5,
                DealtAtPhase = nameof(Phases.Flop),
                IsVisible = true
            });

        game.CurrentPhase = nameof(Phases.Flop);
        await DbContext.SaveChangesAsync();

        // Act: after flop should still use wild-card evaluation
        var flopState = await TableStateBuilder.BuildPrivateStateAsync(game.Id, heroEmail);

        // Assert
        flopState.Should().NotBeNull();
        flopState!.HandEvaluationDescription.Should().Be("Three of a kind, Queens");
    }

    [Fact]
    public async Task BuildPublicStateAsync_SevenCardStud_ShowsBoardCards()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SEVENCARDSTUD", 4);
        await Mediator.Send(new StartHandCommand(setup.Game.Id));

        // Re-fetch to get updated state
        var game = await DbContext.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == setup.Game.Id);

        // Deal cards using flow handler
        var handler = FlowHandlerFactory.GetHandler("SEVENCARDSTUD");
        await handler.DealCardsAsync(DbContext, game, setup.GamePlayers, DateTimeOffset.UtcNow, CancellationToken.None);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        // Seven Card Stud should show board (up) cards for all players
        result!.Seats.Should().HaveCount(4);
        result.Seats.Should().OnlyContain(s => s.Cards.Any());
        
        // Check that at least one card is face up (visible) for each player
        foreach (var seat in result.Seats)
        {
            seat.Cards.Should().Contain(c => c.IsFaceUp && c.Rank != null && c.Suit != null);
            // And ensure we have face-down cards too (Rank/Suit should be null/hidden)
            seat.Cards.Should().Contain(c => !c.IsFaceUp && c.Rank == null && c.Suit == null);
        }
    }

    [Fact]
    public async Task BuildPublicStateAsync_KingsAndLows_InDropOrStayPhase()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "KINGSANDLOWS", 4);
        await Mediator.Send(new CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.StartHand.StartHandCommand(setup.Game.Id));

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CurrentPhase.Should().Be(nameof(Phases.DropOrStay));
    }

    [Fact]
    public async Task BuildPublicStateAsync_FollowTheQueen_DynamicWildRank_UsesNextCardAfterQueenInGlobalDealOrder()
    {
        // Arrange
        // Repro: If DealOrder resets per street for visible board cards, a later street card can end up
        // interleaved ahead of earlier street cards when sorting only by DealOrder.
        // We expect the "follow" wild rank to be the card dealt immediately after the last face-up Queen
        // in true deal sequence (street order + rotation), not "the next card dealt to the Queen's owner".
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FOLLOWTHEQUEEN", 2);

        var game = setup.Game;
        game.CurrentHandNumber = 1;
        game.CurrentPhase = "FourthStreet";
        game.DealerPosition = 1; // Seat 1 is dealer, so dealing starts at seat 0 (left of dealer)
        await DbContext.SaveChangesAsync();

        var rob = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
        var lynne = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);

        // Third street visible cards (DealOrder 1..N within street)
        DbContext.GameCards.Add(new GameCard
        {
            GameId = game.Id,
            GamePlayerId = rob.Id,
            HandNumber = game.CurrentHandNumber,
            Location = CardLocation.Board,
            Suit = CardSuit.Clubs,
            Symbol = CardSymbol.Queen,
            DealtAtPhase = "ThirdStreet",
            DealOrder = 1,
            IsVisible = true
        });
        DbContext.GameCards.Add(new GameCard
        {
            GameId = game.Id,
            GamePlayerId = lynne.Id,
            HandNumber = game.CurrentHandNumber,
            Location = CardLocation.Board,
            Suit = CardSuit.Hearts,
            Symbol = CardSymbol.Four,
            DealtAtPhase = "ThirdStreet",
            DealOrder = 2,
            IsVisible = true
        });

        // Fourth street visible card with DealOrder reset back to 1
        DbContext.GameCards.Add(new GameCard
        {
            GameId = game.Id,
            GamePlayerId = rob.Id,
            HandNumber = game.CurrentHandNumber,
            Location = CardLocation.Board,
            Suit = CardSuit.Hearts,
            Symbol = CardSymbol.Ace,
            DealtAtPhase = "FourthStreet",
            DealOrder = 1,
            IsVisible = true
        });

        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.SpecialRules.Should().NotBeNull();
        result.SpecialRules!.WildCardRules.Should().NotBeNull();
        result.SpecialRules.WildCardRules!.WildRanks.Should().NotBeNull();
        result.SpecialRules.WildCardRules.WildRanks!.Should().Contain("Q");
        result.SpecialRules.WildCardRules.WildRanks!.Should().Contain("4");
        result.SpecialRules.WildCardRules.WildRanks!.Should().NotContain("A");
    }

    [Fact]
    public async Task BuildPublicStateAsync_DealersChoiceGame_SetsIsDealersChoice()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateDealersChoiceGameSetupAsync(DbContext, 3);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsDealersChoice.Should().BeTrue();
    }

    [Fact]
    public async Task BuildPublicStateAsync_DealersChoiceGame_SetsDealersChoiceDealerPosition()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateDealersChoiceGameSetupAsync(DbContext, 3, dealerSeatPosition: 1);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.DealersChoiceDealerPosition.Should().Be(1);
    }

    [Fact]
    public async Task BuildPublicStateAsync_NonDealersChoiceGame_DealersChoiceFieldsNotSet()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 3);

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsDealersChoice.Should().BeFalse();
        result.DealersChoiceDealerPosition.Should().BeNull();
    }
}
