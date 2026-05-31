using CardGames.Poker.Api.Services;

namespace CardGames.IntegrationTests.Services;

/// <summary>
/// Targeted safety-net tests for <see cref="TableStateBuilder"/> table-state assembly.
/// These lock down current projection behavior (visibility, seat shaping, sitting-out
/// reasons, disconnected/late-joiner handling) so future decomposition/refactoring of the
/// builder does not silently change observable behavior.
/// </summary>
public class TableStateAssemblyTests : IntegrationTestBase
{
    private ITableStateBuilder TableStateBuilder => Scope.ServiceProvider.GetRequiredService<ITableStateBuilder>();

    private async Task<List<GamePlayer>> LoadGamePlayersAsync(Guid gameId) =>
        await DbContext.GamePlayers
            .Include(gp => gp.Player)
            .Where(gp => gp.GameId == gameId)
            .OrderBy(gp => gp.SeatPosition)
            .ToListAsync();

    // ---------------------------------------------------------------------
    // Disconnected player behavior
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BuildPublicStateAsync_DisconnectedPlayer_SeatIsMarkedDisconnected()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var players = await LoadGamePlayersAsync(setup.Game.Id);
        players[1].IsConnected = false;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        var connectedSeat = result!.Seats.Single(s => s.SeatIndex == players[0].SeatPosition);
        var disconnectedSeat = result.Seats.Single(s => s.SeatIndex == players[1].SeatPosition);

        connectedSeat.IsDisconnected.Should().BeFalse("a connected player must not be flagged as disconnected");
        disconnectedSeat.IsDisconnected.Should().BeTrue("IsDisconnected projects the inverse of GamePlayer.IsConnected");
    }

    // ---------------------------------------------------------------------
    // Left players are excluded from table state
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BuildPublicStateAsync_PlayerWhoLeft_IsExcludedFromSeats()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 3);
        var players = await LoadGamePlayersAsync(setup.Game.Id);
        players[2].Status = GamePlayerStatus.Left;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Seats.Should().HaveCount(2, "players who have left the game are not projected into table state");
        result.Seats.Should().NotContain(s => s.SeatIndex == players[2].SeatPosition);
    }

    // ---------------------------------------------------------------------
    // Late-joiner / sitting-out reason shaping
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BuildPublicStateAsync_LateJoinerFoldedThisHand_IsShownAsObserving()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, ante: 10);
        setup.Game.CurrentHandNumber = 5;

        var players = await LoadGamePlayersAsync(setup.Game.Id);
        // Player joined on the current hand and folded immediately (late joiner observing this hand).
        players[1].JoinedAtHandNumber = 5;
        players[1].HasFolded = true;
        players[1].IsSittingOut = false;
        players[1].Status = GamePlayerStatus.Active;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        var seat = result!.Seats.Single(s => s.SeatIndex == players[1].SeatPosition);
        seat.SittingOutReason.Should().Be("Observing",
            "a player who folded on the same hand they joined is observing rather than actively playing");
    }

    [Fact]
    public async Task BuildPublicStateAsync_FoldedEstablishedPlayer_HasNoSittingOutReason()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, ante: 10);
        setup.Game.CurrentHandNumber = 5;

        var players = await LoadGamePlayersAsync(setup.Game.Id);
        // Player joined earlier and folded this hand; this is normal folding, not observing.
        players[1].JoinedAtHandNumber = 1;
        players[1].HasFolded = true;
        players[1].IsSittingOut = false;
        players[1].Status = GamePlayerStatus.Active;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        var seat = result!.Seats.Single(s => s.SeatIndex == players[1].SeatPosition);
        seat.IsFolded.Should().BeTrue();
        seat.SittingOutReason.Should().BeNull(
            "an established player who folds is not classed as observing/sitting out");
    }

    [Fact]
    public async Task BuildPublicStateAsync_SittingOutWithInsufficientChips_ReportsInsufficientChips()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, ante: 50);
        var players = await LoadGamePlayersAsync(setup.Game.Id);
        players[1].IsSittingOut = true;
        players[1].ChipStack = 10; // below the 50 ante
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        var seat = result!.Seats.Single(s => s.SeatIndex == players[1].SeatPosition);
        seat.IsSittingOut.Should().BeTrue();
        seat.SittingOutReason.Should().Be("Insufficient chips");
    }

    [Fact]
    public async Task BuildPublicStateAsync_SittingOutWithEnoughChips_ReportsSittingOut()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, ante: 50);
        var players = await LoadGamePlayersAsync(setup.Game.Id);
        players[1].IsSittingOut = true;
        players[1].ChipStack = 500; // comfortably above the ante
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        var seat = result!.Seats.Single(s => s.SeatIndex == players[1].SeatPosition);
        seat.IsSittingOut.Should().BeTrue();
        seat.SittingOutReason.Should().Be("Sitting out");
    }

    // ---------------------------------------------------------------------
    // Showdown card visibility: folded hidden, staying revealed
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BuildPublicStateAsync_Showdown_RevealsStayingPlayerButHidesFoldedPlayerCards()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2, ante: 10);
        var game = setup.Game;
        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.Complete);
        game.Status = GameStatus.InProgress;

        var players = await LoadGamePlayersAsync(game.Id);
        var stayer = players[0];
        var folder = players[1];
        folder.HasFolded = true;
        stayer.HasFolded = false;

        AddHand(game.Id, stayer.Id, CardSuit.Hearts, CardSymbol.Ace, CardSuit.Spades, CardSymbol.King);
        AddHand(game.Id, folder.Id, CardSuit.Clubs, CardSymbol.Deuce, CardSuit.Diamonds, CardSymbol.Three);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(game.Id);

        // Assert
        result.Should().NotBeNull();
        var stayerSeat = result!.Seats.Single(s => s.SeatIndex == stayer.SeatPosition);
        var folderSeat = result.Seats.Single(s => s.SeatIndex == folder.SeatPosition);

        stayerSeat.Cards.Should().NotBeEmpty();
        stayerSeat.Cards.Should().AllSatisfy(card =>
        {
            card.IsFaceUp.Should().BeTrue("staying players reveal their cards during showdown");
            card.Rank.Should().NotBeNull();
            card.Suit.Should().NotBeNull();
        });

        folderSeat.Cards.Should().NotBeEmpty();
        folderSeat.Cards.Should().AllSatisfy(card =>
        {
            card.IsFaceUp.Should().BeFalse("folded players never reveal their cards at showdown");
            card.Rank.Should().BeNull();
            card.Suit.Should().BeNull();
        });
    }

    // ---------------------------------------------------------------------
    // Variant-specific public hand-evaluation description (Screw Your Neighbor)
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData("SYN_KEPT", "Kept")]
    [InlineData("SYN_TRADED", "Traded")]
    public async Task BuildPublicStateAsync_ScrewYourNeighbor_ProjectsKeptOrTradedDescription(
        string variantState, string expectedDescription)
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "SCREWYOURNEIGHBOR", 3, ante: 0);
        var players = await LoadGamePlayersAsync(setup.Game.Id);
        players[0].VariantState = variantState;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        var seat = result!.Seats.Single(s => s.SeatIndex == players[0].SeatPosition);
        seat.HandEvaluationDescription.Should().Be(expectedDescription);
    }

    [Fact]
    public async Task BuildPublicStateAsync_NonScrewYourNeighbor_DoesNotProjectKeptTradedDescription()
    {
        // Arrange: VariantState is only translated to Kept/Traded for Screw Your Neighbor.
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var players = await LoadGamePlayersAsync(setup.Game.Id);
        players[0].VariantState = "SYN_KEPT";
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPublicStateAsync(setup.Game.Id);

        // Assert
        result.Should().NotBeNull();
        var seat = result!.Seats.Single(s => s.SeatIndex == players[0].SeatPosition);
        seat.HandEvaluationDescription.Should().BeNull(
            "the Kept/Traded projection is specific to Screw Your Neighbor");
    }

    // ---------------------------------------------------------------------
    // Private vs public visibility
    // ---------------------------------------------------------------------

    [Fact]
    public async Task BuildPrivateStateAsync_OwnFaceDownCards_AreRevealedToOwnerButMarkedNotPubliclyVisible()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var game = setup.Game;
        game.CurrentHandNumber = 1;
        game.CurrentPhase = nameof(Phases.FirstBettingRound);
        game.Status = GameStatus.InProgress;

        var players = await LoadGamePlayersAsync(game.Id);
        var owner = players[0];

        // Face-down hole cards (IsVisible: false) for the owner.
        AddHand(game.Id, owner.Id, CardSuit.Hearts, CardSymbol.Ace, CardSuit.Spades, CardSymbol.King, isVisible: false);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(game.Id, owner.Player.Email!);

        // Assert
        result.Should().NotBeNull();
        result!.Hand.Should().HaveCount(2, "owners always see their own cards even when publicly face-down");
        result.Hand.Should().AllSatisfy(card =>
        {
            card.Rank.Should().NotBeNullOrEmpty();
            card.Suit.Should().NotBeNullOrEmpty();
            card.IsPubliclyVisible.Should().BeFalse("face-down hole cards are not publicly visible to opponents");
        });
    }

    [Fact]
    public async Task BuildPrivateStateAsync_RequestingUserNotInGame_ReturnsNull()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);

        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, "stranger@nowhere.test");

        // Assert
        result.Should().BeNull("private state is only built for players who belong to the game");
    }

    [Fact]
    public async Task BuildPrivateStateAsync_PlayerWhoLeft_ReturnsNull()
    {
        // Arrange
        var setup = await DatabaseSeeder.CreateCompleteGameSetupAsync(DbContext, "FIVECARDDRAW", 2);
        var players = await LoadGamePlayersAsync(setup.Game.Id);
        var leaver = players[1];
        leaver.Status = GamePlayerStatus.Left;
        await DbContext.SaveChangesAsync();

        // Act
        var result = await TableStateBuilder.BuildPrivateStateAsync(setup.Game.Id, leaver.Player.Email!);

        // Assert
        result.Should().BeNull("a player who has left the game no longer receives private state");
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private void AddHand(
        Guid gameId,
        Guid gamePlayerId,
        CardSuit suit1, CardSymbol symbol1,
        CardSuit suit2, CardSymbol symbol2,
        bool isVisible = false)
    {
        DbContext.GameCards.AddRange(
            new GameCard
            {
                GameId = gameId,
                GamePlayerId = gamePlayerId,
                HandNumber = 1,
                Location = CardLocation.Hand,
                Suit = suit1,
                Symbol = symbol1,
                DealOrder = 1,
                IsVisible = isVisible
            },
            new GameCard
            {
                GameId = gameId,
                GamePlayerId = gamePlayerId,
                HandNumber = 1,
                Location = CardLocation.Hand,
                Suit = suit2,
                Symbol = symbol2,
                DealOrder = 2,
                IsVisible = isVisible
            });
    }
}
