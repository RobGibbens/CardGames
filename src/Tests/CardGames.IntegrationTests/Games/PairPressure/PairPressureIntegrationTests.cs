using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.Generic.v1.Commands.PerformShowdown;
using CardGames.Poker.Api.GameFlow;
using CardGames.Poker.Api.Services;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CardGames.IntegrationTests.Games.PairPressure;

public class PairPressureIntegrationTests : IntegrationTestBase
{
    private const string PairPressureCode = "PAIRPRESSURE";

    private ITableStateBuilder TableStateBuilder => Scope.ServiceProvider.GetRequiredService<ITableStateBuilder>();

    [Fact]
    public async Task FlowHandler_Resolves_To_A_StudStyle_Handler_And_Follows_Street_Sequence()
    {
        await EnsurePairPressureGameTypeAsync();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, PairPressureCode, 4);

        var handler = FlowHandlerFactory.GetHandler(PairPressureCode);

        handler.Should().NotBeOfType<FiveCardDrawFlowHandler>("Pair Pressure should not fall back to Five Card Draw flow semantics.");
        handler.GameTypeCode.Should().Be(PairPressureCode);
        handler.GetInitialPhase(setup.Game).Should().Be(nameof(Phases.CollectingAntes));

        var dealingConfiguration = handler.GetDealingConfiguration();
        dealingConfiguration.PatternType.Should().Be(DealingPatternType.StreetBased);
        dealingConfiguration.DealingRounds.Should().HaveCount(5);

        handler.GetNextPhase(setup.Game, nameof(Phases.CollectingAntes)).Should().Be(nameof(Phases.ThirdStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.ThirdStreet)).Should().Be(nameof(Phases.FourthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.FourthStreet)).Should().Be(nameof(Phases.FifthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.FifthStreet)).Should().Be(nameof(Phases.SixthStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.SixthStreet)).Should().Be(nameof(Phases.SeventhStreet));
        handler.GetNextPhase(setup.Game, nameof(Phases.SeventhStreet)).Should().Be(nameof(Phases.Showdown));
        handler.GetNextPhase(setup.Game, nameof(Phases.Showdown)).Should().Be(nameof(Phases.Complete));
    }

    [Fact]
    public async Task BuildPublicStateAsync_Tracks_Only_The_Two_Most_Recent_Distinct_Paired_Ranks()
    {
        await EnsurePairPressureGameTypeAsync();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, PairPressureCode, 2);

        var game = setup.Game;
        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.SixthStreet);
        game.Status = GameStatus.InProgress;
        game.DealerPosition = 1;

        var seatZero = setup.GamePlayers.Single(gp => gp.SeatPosition == 0);
        var seatOne = setup.GamePlayers.Single(gp => gp.SeatPosition == 1);

        AddStudCard(game.Id, seatZero.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Eight, true, nameof(Phases.ThirdStreet), 1);
        AddStudCard(game.Id, seatOne.Id, 1, CardLocation.Board, CardSuit.Diamonds, CardSymbol.Eight, true, nameof(Phases.ThirdStreet), 2);
        AddStudCard(game.Id, seatZero.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.Five, true, nameof(Phases.FourthStreet), 1);
        AddStudCard(game.Id, seatOne.Id, 1, CardLocation.Board, CardSuit.Spades, CardSymbol.Five, true, nameof(Phases.FourthStreet), 2);
        AddStudCard(game.Id, seatZero.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.King, true, nameof(Phases.FifthStreet), 1);
        AddStudCard(game.Id, seatOne.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.King, true, nameof(Phases.FifthStreet), 2);
        AddStudCard(game.Id, seatZero.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Ace, true, nameof(Phases.SixthStreet), 1);
        AddStudCard(game.Id, seatOne.Id, 1, CardLocation.Board, CardSuit.Diamonds, CardSymbol.Queen, true, nameof(Phases.SixthStreet), 2);

        await DbContext.SaveChangesAsync();

        var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

        result.Should().NotBeNull();
        result!.SpecialRules.Should().NotBeNull();
        result.SpecialRules!.WildCardRules.Should().NotBeNull();
        result.SpecialRules.WildCardRules!.WildRanks.Should().NotBeNull();
        result.SpecialRules.WildCardRules.WildRanks!.Should().HaveCount(2);
        result.SpecialRules.WildCardRules.WildRanks!.Should().Contain("5");
        result.SpecialRules.WildCardRules.WildRanks!.Should().Contain("K");
        result.SpecialRules.WildCardRules.WildRanks!.Should().NotContain("8");
    }

    [Fact]
    public async Task PerformShowdown_Uses_PairPressure_WildCard_Evaluation()
    {
        await EnsurePairPressureGameTypeAsync();
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, PairPressureCode, 2, ante: 10);

        var game = setup.Game;
        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.Showdown);
        game.Status = GameStatus.InProgress;

        await DatabaseSeeder.CreatePotAsync(DbContext, game, 100);

        var hero = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).First();
        var villain = setup.GamePlayers.OrderBy(gp => gp.SeatPosition).Last();
        var now = DateTimeOffset.UtcNow;

        AddStudCard(game.Id, hero.Id, 1, CardLocation.Hole, CardSuit.Hearts, CardSymbol.Five, false, nameof(Phases.ThirdStreet), 1, now);
        AddStudCard(game.Id, hero.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.King, false, nameof(Phases.ThirdStreet), 2, now);
        AddStudCard(game.Id, hero.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.Eight, true, nameof(Phases.ThirdStreet), 3, now);
        AddStudCard(game.Id, hero.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.Five, true, nameof(Phases.FourthStreet), 4, now);
        AddStudCard(game.Id, hero.Id, 1, CardLocation.Board, CardSuit.Hearts, CardSymbol.King, true, nameof(Phases.FifthStreet), 5, now);
        AddStudCard(game.Id, hero.Id, 1, CardLocation.Board, CardSuit.Spades, CardSymbol.Ace, true, nameof(Phases.SixthStreet), 6, now);
        AddStudCard(game.Id, hero.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.Ace, false, nameof(Phases.SeventhStreet), 7, now);

        AddStudCard(game.Id, villain.Id, 1, CardLocation.Hole, CardSuit.Hearts, CardSymbol.Queen, false, nameof(Phases.ThirdStreet), 1, now);
        AddStudCard(game.Id, villain.Id, 1, CardLocation.Hole, CardSuit.Diamonds, CardSymbol.Jack, false, nameof(Phases.ThirdStreet), 2, now);
        AddStudCard(game.Id, villain.Id, 1, CardLocation.Board, CardSuit.Diamonds, CardSymbol.Eight, true, nameof(Phases.ThirdStreet), 3, now);
        AddStudCard(game.Id, villain.Id, 1, CardLocation.Board, CardSuit.Spades, CardSymbol.Five, true, nameof(Phases.FourthStreet), 4, now);
        AddStudCard(game.Id, villain.Id, 1, CardLocation.Board, CardSuit.Clubs, CardSymbol.King, true, nameof(Phases.FifthStreet), 5, now);
        AddStudCard(game.Id, villain.Id, 1, CardLocation.Board, CardSuit.Diamonds, CardSymbol.Queen, true, nameof(Phases.SixthStreet), 6, now);
        AddStudCard(game.Id, villain.Id, 1, CardLocation.Hole, CardSuit.Clubs, CardSymbol.Deuce, false, nameof(Phases.SeventhStreet), 7, now);

        await DbContext.SaveChangesAsync();

        var result = await Mediator.Send(new PerformShowdownCommand(game.Id));

        result.IsT0.Should().BeTrue();
        result.AsT0.CurrentPhase.Should().Be(nameof(Phases.Complete));
        result.AsT0.Payouts.Should().ContainKey(hero.Player.Name);
        result.AsT0.Payouts.Should().NotContainKey(villain.Player.Name);
        result.AsT0.PlayerHands.Should().ContainSingle(hand => hand.PlayerName == hero.Player.Name && hand.IsWinner);
    }

    private async Task EnsurePairPressureGameTypeAsync()
    {
        var existing = await DbContext.GameTypes.FirstOrDefaultAsync(gameType => gameType.Code == PairPressureCode);
        if (existing is not null)
        {
            return;
        }

        DbContext.GameTypes.Add(new GameType
        {
            Id = Guid.CreateVersion7(),
            Code = PairPressureCode,
            Name = "Pair Pressure",
            MinPlayers = 2,
            MaxPlayers = 8,
            InitialHoleCards = 2,
            InitialBoardCards = 1,
            MaxCommunityCards = 0,
            MaxPlayerCards = 7,
            BettingStructure = BettingStructure.Ante
        });

        await DbContext.SaveChangesAsync();
    }

    private void AddStudCard(
        Guid gameId,
        Guid gamePlayerId,
        int handNumber,
        CardLocation location,
        CardSuit suit,
        CardSymbol symbol,
        bool isVisible,
        string dealtAtPhase,
        int dealOrder,
        DateTimeOffset? dealtAt = null)
    {
        DbContext.GameCards.Add(new GameCard
        {
            Id = Guid.CreateVersion7(),
            GameId = gameId,
            GamePlayerId = gamePlayerId,
            HandNumber = handNumber,
            Location = location,
            Suit = suit,
            Symbol = symbol,
            DealOrder = dealOrder,
            IsVisible = isVisible,
            IsDiscarded = false,
            DealtAtPhase = dealtAtPhase,
            DealtAt = dealtAt ?? DateTimeOffset.UtcNow
        });
    }
}