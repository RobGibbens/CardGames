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
        // In public state, cards should remain face down without rank/suit payloads.
        result!.Seats.Should().AllSatisfy(seat =>
        {
            seat.Cards.Should().NotBeNull();
            seat.Cards.Should().AllSatisfy(card =>
            {
                card.IsFaceUp.Should().BeFalse();
                card.Rank.Should().BeNull();
                card.Suit.Should().BeNull();
            });
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
    public async Task BuildPublicStateAsync_ScrewYourNeighbor_VisibleKingIsShownToAllPlayers()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 3);
        var game = setup.Game;
        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.KeepOrTrade);
        game.Status = GameStatus.InProgress;

        var seat0 = setup.GamePlayers.First(gp => gp.SeatPosition == 0);
        var seat1 = setup.GamePlayers.First(gp => gp.SeatPosition == 1);

        DbContext.GameCards.AddRange(
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = seat0.Id,
                HandNumber = 1,
                Location = CardLocation.Hand,
                Suit = CardSuit.Spades,
                Symbol = CardSymbol.King,
                DealOrder = 1,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = seat1.Id,
                HandNumber = 1,
                Location = CardLocation.Hand,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.Five,
                DealOrder = 1,
                IsVisible = false
            });

        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

        // Assert
        result.Should().NotBeNull();
        var seat0Public = result!.Seats.First(s => s.SeatIndex == 0);
        var seat1Public = result.Seats.First(s => s.SeatIndex == 1);

        seat0Public.Cards.Should().ContainSingle();
        seat0Public.Cards[0].IsFaceUp.Should().BeTrue();
        seat0Public.Cards[0].Rank.Should().Be("K");
        seat0Public.Cards[0].Suit.Should().NotBeNull();

        seat1Public.Cards.Should().ContainSingle();
        seat1Public.Cards[0].IsFaceUp.Should().BeFalse();
        seat1Public.Cards[0].Rank.Should().BeNull();
        seat1Public.Cards[0].Suit.Should().BeNull();
    }

    [Fact]
    public async Task BuildPublicStateAsync_ScrewYourNeighbor_EndedGameStillIncludesCompleteShowdownForWinner()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext,
            "SCREWYOURNEIGHBOR",
            2,
            startingChips: 25,
            ante: 25);

        await Mediator.Send(new CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand.StartHandCommand(setup.Game.Id));

        var game = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == setup.Game.Id);

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var winner = players[0];
        var loser = players[1];

        var handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        handCards.First(c => c.GamePlayerId == winner.Id).Symbol = CardSymbol.Four;
        handCards.First(c => c.GamePlayerId == winner.Id).Suit = CardSuit.Hearts;
        handCards.First(c => c.GamePlayerId == loser.Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == loser.Id).Suit = CardSuit.Spades;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 2; i++)
        {
            game = await DbContext.Games
                .Include(g => g.GamePlayers)
                .AsNoTracking()
                .FirstAsync(g => g.Id == setup.Game.Id);

            if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
            {
                break;
            }

            var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade.KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
            keepResult.IsT0.Should().BeTrue();
        }

        var showdownResult = await Mediator.Send(new CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommand(setup.Game.Id));
        showdownResult.IsT0.Should().BeTrue();

        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        result.Should().NotBeNull();
        result!.CurrentPhase.Should().Be("Ended");
        result.Showdown.Should().NotBeNull();
        result.Showdown!.IsComplete.Should().BeTrue();
        result.Showdown.PlayerResults.Should().ContainSingle(r => r.PlayerName == winner.Player.Name && r.IsWinner);
        result.Showdown.PlayerResults.Should().ContainSingle(r => r.PlayerName == loser.Player.Name && !r.IsWinner);
    }

    [Fact]
    public async Task BuildPublicStateAsync_ScrewYourNeighbor_ShowdownExcludesZeroChipSitOutWithoutHand()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext,
            "SCREWYOURNEIGHBOR",
            3,
            startingChips: 25,
            ante: 25);

        var game = setup.Game;
        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.Showdown);
        game.Status = GameStatus.InProgress;

        var players = await DbContext.GamePlayers
            .Include(gp => gp.Player)
            .Where(gp => gp.GameId == game.Id)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

        var winner = players[0];
        var bustedLoser = players[1];
        var sittingOut = players[2];

        bustedLoser.ChipStack = 0;
        bustedLoser.IsSittingOut = true;
        sittingOut.ChipStack = 0;
        sittingOut.IsSittingOut = true;

        DbContext.GameCards.AddRange(
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = winner.Id,
                HandNumber = 1,
                Location = CardLocation.Hand,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.Four,
                DealOrder = 1,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = bustedLoser.Id,
                HandNumber = 1,
                Location = CardLocation.Hand,
                Suit = CardSuit.Spades,
                Symbol = CardSymbol.Ace,
                DealOrder = 1,
                IsVisible = true
            });

        await DbContext.SaveChangesAsync();

        var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

        result.Should().NotBeNull();
        result!.Showdown.Should().NotBeNull();
        result.Showdown!.PlayerResults.Should().HaveCount(2);
        result.Showdown.PlayerResults.Should().ContainSingle(r => r.PlayerName == winner.Player!.Name && r.IsWinner);
        result.Showdown.PlayerResults.Should().ContainSingle(r => r.PlayerName == bustedLoser.Player!.Name && !r.IsWinner);
        result.Showdown.PlayerResults.Should().NotContain(r => r.PlayerName == sittingOut.Player!.Name);
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

    [Fact]
    public async Task BuildPublicStateAsync_SouthDakotaShowdown_BestCardIndexesUseThreeHoleAndTwoCommunity()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SOUTHDAKOTA", 2);
        var game = setup.Game;
        var hero = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).First();
        var villain = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).Last();

        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.Showdown);
        game.Status = GameStatus.InProgress;

        // Hero hole cards (5): Ah As Kd Qc 2d
        DbContext.GameCards.AddRange(
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = hero.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.Ace,
                DealOrder = 1,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = hero.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Spades,
                Symbol = CardSymbol.Ace,
                DealOrder = 2,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = hero.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Diamonds,
                Symbol = CardSymbol.King,
                DealOrder = 3,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = hero.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Clubs,
                Symbol = CardSymbol.Queen,
                DealOrder = 4,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = hero.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Diamonds,
                Symbol = CardSymbol.Deuce,
                DealOrder = 5,
                IsVisible = true
            });

        // Villain hole cards (5): all low cards
        DbContext.GameCards.AddRange(
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = villain.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Clubs,
                Symbol = CardSymbol.Three,
                DealOrder = 1,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = villain.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Spades,
                Symbol = CardSymbol.Four,
                DealOrder = 2,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = villain.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.Five,
                DealOrder = 3,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = villain.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Diamonds,
                Symbol = CardSymbol.Six,
                DealOrder = 4,
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                GamePlayerId = villain.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Hand,
                Suit = CardSuit.Clubs,
                Symbol = CardSymbol.Seven,
                DealOrder = 5,
                IsVisible = true
            });

        // Community cards (3 total for South Dakota): Kh Qh Jc
        DbContext.GameCards.AddRange(
            new GameCard
            {
                GameId = game.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Community,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.King,
                DealOrder = 1,
                DealtAtPhase = nameof(Phases.Flop),
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Community,
                Suit = CardSuit.Hearts,
                Symbol = CardSymbol.Queen,
                DealOrder = 2,
                DealtAtPhase = nameof(Phases.Flop),
                IsVisible = true
            },
            new GameCard
            {
                GameId = game.Id,
                HandNumber = game.CurrentHandNumber,
                Location = CardLocation.Community,
                Suit = CardSuit.Clubs,
                Symbol = CardSymbol.Jack,
                DealOrder = 3,
                DealtAtPhase = nameof(Phases.Turn),
                IsVisible = true
            });

        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Showdown.Should().NotBeNull();

        var heroResult = result.Showdown!.PlayerResults.FirstOrDefault(p => p.PlayerName == hero.Player.Name);
        heroResult.Should().NotBeNull();
        heroResult!.Cards.Should().HaveCount(8, "5 hole cards + 3 community cards should be present at South Dakota showdown");
        heroResult.BestCardIndexes.Should().NotBeNull();
        heroResult.BestCardIndexes!.Should().HaveCount(5);

        var holeIndexes = heroResult.BestCardIndexes!.Where(i => i is >= 0 and <= 4).ToList();
        var communityIndexes = heroResult.BestCardIndexes!.Where(i => i >= 5).ToList();

        holeIndexes.Should().HaveCount(3, "South Dakota must highlight exactly 3 hole cards in the best hand");
        communityIndexes.Should().HaveCount(2, "South Dakota must highlight exactly 2 community cards in the best hand");
    }

    [Fact]
    public async Task BuildPublicStateAsync_CompletedTenthHand_IncludesDeterministicWinningSoundCue()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext,
            "SCREWYOURNEIGHBOR",
            2,
            startingChips: 25,
            ante: 25);

        setup.Game.CurrentHandNumber = 9;
        await DbContext.SaveChangesAsync();

        await Mediator.Send(new CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand.StartHandCommand(setup.Game.Id));

        var game = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == setup.Game.Id);

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var winner = players[0];
        var loser = players[1];

        var handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        handCards.First(c => c.GamePlayerId == winner.Id).Symbol = CardSymbol.Four;
        handCards.First(c => c.GamePlayerId == winner.Id).Suit = CardSuit.Hearts;
        handCards.First(c => c.GamePlayerId == loser.Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == loser.Id).Suit = CardSuit.Spades;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 2; i++)
        {
            game = await DbContext.Games
                .Include(g => g.GamePlayers)
                .AsNoTracking()
                .FirstAsync(g => g.Id == setup.Game.Id);

            if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
            {
                break;
            }

            var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade.KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
            keepResult.IsT0.Should().BeTrue();
        }

        var showdownResult = await Mediator.Send(new CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommand(setup.Game.Id));
        showdownResult.IsT0.Should().BeTrue();

        var firstResult = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);
        var secondResult = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        firstResult.Should().NotBeNull();
        firstResult!.SoundEffects.Should().ContainSingle();
        firstResult.SoundEffects![0].EventKey.Should().Be("winning");
        firstResult.SoundEffects[0].HandNumber.Should().Be(10);
        firstResult.SoundEffects[0].CueKey.Should().StartWith("winning:10:");
        firstResult.SoundEffects[0].Source.Should().Be("/sounds/soundboard/winning/pay_dat_man_his_money.mp3");

        secondResult.Should().NotBeNull();
        secondResult!.SoundEffects.Should().ContainSingle();
        secondResult.SoundEffects![0].Source.Should().Be(firstResult.SoundEffects[0].Source);
        secondResult.SoundEffects[0].CueKey.Should().Be(firstResult.SoundEffects[0].CueKey);
    }

    [Fact]
    public async Task BuildPublicStateAsync_CompletedNonTenthHand_DoesNotIncludeWinningSoundCue()
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(
            DbContext,
            "SCREWYOURNEIGHBOR",
            2,
            startingChips: 25,
            ante: 25);

        await Mediator.Send(new CardGames.Poker.Api.Features.Games.Generic.v1.Commands.StartHand.StartHandCommand(setup.Game.Id));

        var game = await DbContext.Games
            .Include(g => g.GamePlayers).ThenInclude(gp => gp.Player)
            .FirstAsync(g => g.Id == setup.Game.Id);

        var players = game.GamePlayers.OrderBy(gp => gp.SeatPosition).ToList();
        var winner = players[0];
        var loser = players[1];

        var handCards = await DbContext.GameCards
            .Where(gc => gc.GameId == game.Id &&
                         gc.HandNumber == game.CurrentHandNumber &&
                         gc.Location == CardLocation.Hand &&
                         gc.GamePlayerId != null)
            .ToListAsync();

        handCards.First(c => c.GamePlayerId == winner.Id).Symbol = CardSymbol.Four;
        handCards.First(c => c.GamePlayerId == winner.Id).Suit = CardSuit.Hearts;
        handCards.First(c => c.GamePlayerId == loser.Id).Symbol = CardSymbol.Ace;
        handCards.First(c => c.GamePlayerId == loser.Id).Suit = CardSuit.Spades;
        await DbContext.SaveChangesAsync();

        for (var i = 0; i < 2; i++)
        {
            game = await DbContext.Games
                .Include(g => g.GamePlayers)
                .AsNoTracking()
                .FirstAsync(g => g.Id == setup.Game.Id);

            if (game.CurrentPhase != nameof(Phases.KeepOrTrade))
            {
                break;
            }

            var currentActor = game.GamePlayers.First(gp => gp.SeatPosition == game.CurrentPlayerIndex);
            var keepResult = await Mediator.Send(new CardGames.Poker.Api.Features.Games.ScrewYourNeighbor.v1.Commands.KeepOrTrade.KeepOrTradeCommand(game.Id, currentActor.PlayerId, "Keep"));
            keepResult.IsT0.Should().BeTrue();
        }

        var showdownResult = await Mediator.Send(new CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown.PerformShowdownCommand(setup.Game.Id));
        showdownResult.IsT0.Should().BeTrue();

        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        result.Should().NotBeNull();
        result!.SoundEffects.Should().BeNullOrEmpty();
    }
}
