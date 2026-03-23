using CardGames.IntegrationTests.Infrastructure;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;
using CardGames.Poker.Betting;
using Microsoft.EntityFrameworkCore;

namespace CardGames.IntegrationTests.Games.BobBarker;

public class BobBarkerShowcaseSelectionTests : IntegrationTestBase
{
    [Fact]
    public async Task SelectShowcase_AllowsOutOfOrderSelection_BySeatIndex()
    {
        var setup = await CreateBobBarkerGameInDrawPhaseAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        var freshDb = GetFreshDbContext();
        var freshGame = await freshDb.Games
            .Include(g => g.GamePlayers)
            .FirstAsync(g => g.Id == game.Id);

        var nonCurrentSeat = freshGame.GamePlayers
            .Where(gp => !gp.HasFolded && gp.Status == GamePlayerStatus.Active && gp.SeatPosition != freshGame.CurrentDrawPlayerIndex)
            .OrderBy(gp => gp.SeatPosition)
            .Select(gp => gp.SeatPosition)
            .First();

        var selectResult = await Mediator.Send(new SelectShowcaseCommand(game.Id, ShowcaseCardIndex: 1, PlayerSeatIndex: nonCurrentSeat));

        selectResult.IsT0.Should().BeTrue("an eligible Bob Barker player should be able to choose a showcase card without waiting for seat order");
        selectResult.AsT0.PlayerSeatIndex.Should().Be(nonCurrentSeat);

        var selectedPlayer = await GetFreshDbContext().GamePlayers
            .FirstAsync(gp => gp.GameId == game.Id && gp.SeatPosition == nonCurrentSeat);
        selectedPlayer.HasDrawnThisRound.Should().BeTrue();
    }

    [Fact]
    public async Task SelectShowcase_PartialCompletion_StaysInDrawPhaseUntilAllPlayersAct()
    {
        var setup = await CreateBobBarkerGameInDrawPhaseAsync(playerCount: 3, dealerPosition: 0);
        var game = setup.Game;

        var firstResult = await Mediator.Send(new SelectShowcaseCommand(game.Id, ShowcaseCardIndex: 0, PlayerSeatIndex: 2));
        firstResult.IsT0.Should().BeTrue();

        var afterOneSelection = await GetFreshDbContext().Games.FirstAsync(g => g.Id == game.Id);
        afterOneSelection.CurrentPhase.Should().Be(nameof(Phases.DrawPhase), "Bob Barker should wait for all eligible players to choose a showcase card before pre-flop");
    }

    private async Task<GameSetup> CreateBobBarkerGameInDrawPhaseAsync(int playerCount, int dealerPosition)
    {
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "BOBBARKER", playerCount, startingChips: 1000, ante: 0);
        var game = setup.Game;
        var now = DateTimeOffset.UtcNow;

        game.CurrentHandNumber = 1;
        game.CurrentHandGameTypeCode = "BOBBARKER";
        game.CurrentPhase = nameof(Phases.DrawPhase);
        game.Status = GameStatus.InProgress;
        game.DealerPosition = dealerPosition;
        game.CurrentDrawPlayerIndex = setup.GamePlayers
            .Where(gp => gp.Status == GamePlayerStatus.Active && !gp.HasFolded)
            .OrderBy(gp => gp.SeatPosition)
            .Select(gp => gp.SeatPosition)
            .First();
        game.CurrentPlayerIndex = game.CurrentDrawPlayerIndex;

        foreach (var gamePlayer in setup.GamePlayers)
        {
            gamePlayer.HasDrawnThisRound = false;

            for (var cardIndex = 0; cardIndex < 5; cardIndex++)
            {
                DbContext.GameCards.Add(new GameCard
                {
                    Id = Guid.CreateVersion7(),
                    GameId = game.Id,
                    GamePlayerId = gamePlayer.Id,
                    HandNumber = game.CurrentHandNumber,
                    Location = CardLocation.Hand,
                    Suit = (CardSuit)(cardIndex % 4),
                    Symbol = (CardSymbol)(cardIndex + 1),
                    DealOrder = cardIndex + 1,
                    IsVisible = false,
                    IsDiscarded = false,
                    DealtAt = now,
                    DealtAtPhase = nameof(Phases.Dealing)
                });
            }
        }

        DbContext.GameCards.Add(new GameCard
        {
            Id = Guid.CreateVersion7(),
            GameId = game.Id,
            HandNumber = game.CurrentHandNumber,
            Location = CardLocation.Community,
            Suit = CardSuit.Spades,
            Symbol = CardSymbol.Jack,
            DealOrder = 0,
            IsVisible = false,
            IsDiscarded = false,
            DealtAt = now,
            DealtAtPhase = nameof(Phases.Dealing)
        });

        await DbContext.SaveChangesAsync();

        return setup;
    }
}