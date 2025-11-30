using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Api.Features.Variants;
using CardGames.Poker.Shared.Contracts.Lobby;
using CardGames.Poker.Shared.Enums;
using CardGames.Poker.Shared.RuleSets;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

/// <summary>
/// End-to-end API tests for Five Card Draw integration.
/// These tests verify the complete API flow for Five Card Draw game management.
/// </summary>
public class FiveCardDrawApiEndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string FiveCardDrawVariantId = "five-card-draw";
    
    private readonly HttpClient _client;

    public FiveCardDrawApiEndToEndTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    #region Variant Discovery Tests

    [Fact]
    public async Task GetVariants_IncludesFiveCardDrawWithCompleteInfo()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        
        var fiveCardDraw = result.Variants!.FirstOrDefault(v => v.Id == FiveCardDrawVariantId);
        fiveCardDraw.Should().NotBeNull();
        fiveCardDraw!.Name.Should().Be("Five Card Draw");
        fiveCardDraw.Description.Should().NotBeNullOrEmpty();
        fiveCardDraw.Description.Should().Contain("5 cards");
        fiveCardDraw.MinPlayers.Should().Be(2);
        fiveCardDraw.MaxPlayers.Should().Be(6);
    }

    [Fact]
    public async Task GetVariantById_FiveCardDraw_ReturnsDetailedInfo()
    {
        // Act
        var response = await _client.GetAsync($"/api/variants/{FiveCardDrawVariantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be(FiveCardDrawVariantId);
        result.Variant.Name.Should().Be("Five Card Draw");
    }

    [Fact]
    public async Task GetVariantById_FiveCardDraw_CaseInsensitive()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/FIVE-CARD-DRAW");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant!.Id.Should().Be(FiveCardDrawVariantId);
    }

    #endregion

    #region Table Creation Tests for Five Card Draw

    [Fact]
    public async Task CreateTable_FiveCardDraw_FixedLimit_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Five Card Draw Table",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 5,
            BigBlind: 10,
            MinBuyIn: 100,
            MaxBuyIn: 500,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.Variant.Should().Be(PokerVariant.FiveCardDraw);
        result.Table.LimitType.Should().Be(LimitType.FixedLimit);
        result.Table.State.Should().Be(GameState.WaitingForPlayers);
    }

    [Fact]
    public async Task CreateTable_FiveCardDraw_NoLimit_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "NL Five Card Draw Table",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 2,
            BigBlind: 4,
            MinBuyIn: 80,
            MaxBuyIn: 400,
            MaxSeats: 5,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.NoLimit);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.LimitType.Should().Be(LimitType.NoLimit);
    }

    [Fact]
    public async Task CreateTable_FiveCardDraw_WithAnte_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Five Card Draw with Ante",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 2,
            BigBlind: 4,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit,
            Ante: 1);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.Ante.Should().Be(1);
    }

    [Fact]
    public async Task CreateTable_FiveCardDraw_HeadsUp_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Heads Up Five Card Draw",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 10,
            BigBlind: 20,
            MinBuyIn: 200,
            MaxBuyIn: 1000,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.MaxSeats.Should().Be(2);
    }

    [Fact]
    public async Task CreateTable_FiveCardDraw_MaxPlayers_Succeeds()
    {
        // Arrange - Five Card Draw supports max 6 players due to card constraints
        var request = new CreateTableRequest(
            Name: "Full Ring Five Card Draw",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 20,
            MaxBuyIn: 100,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.MaxSeats.Should().Be(6);
    }

    [Fact]
    public async Task CreateTable_FiveCardDraw_TooManyPlayers_Fails()
    {
        // Arrange - Five Card Draw max is 6 players (5 initial cards + up to 3 draw = 8 cards max per player; 6 * 8 = 48 < 52)
        var request = new CreateTableRequest(
            Name: "Too Many Players Table",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 20,
            MaxBuyIn: 100,
            MaxSeats: 8,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Table Filtering Tests for Five Card Draw

    [Fact]
    public async Task GetTables_FilterByFiveCardDraw_ReturnsOnlyDrawTables()
    {
        // Act
        var response = await _client.GetAsync($"/api/tables?variant={PokerVariant.FiveCardDraw}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => t.Variant == PokerVariant.FiveCardDraw);
    }

    [Fact]
    public async Task GetTables_FilterByDrawAndStakes_ReturnsFilteredTables()
    {
        // Act
        var response = await _client.GetAsync($"/api/tables?variant={PokerVariant.FiveCardDraw}&minSmallBlind=1&maxSmallBlind=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().OnlyContain(t => 
            t.Variant == PokerVariant.FiveCardDraw &&
            t.SmallBlind >= 1 &&
            t.SmallBlind <= 10);
    }

    #endregion

    #region Quick Join Tests for Five Card Draw

    [Fact]
    public async Task QuickJoin_FilterByFiveCardDraw_JoinsDrawTable()
    {
        // Act
        var request = new QuickJoinRequest(Variant: PokerVariant.FiveCardDraw);
        var response = await _client.PostAsJsonAsync("/api/tables/quick-join", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.Variant.Should().Be(PokerVariant.FiveCardDraw);
    }

    #endregion

    #region Table Configuration Tests

    [Fact]
    public async Task CreateTable_FiveCardDraw_HasCorrectConfig()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Config Verification Table",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 5,
            BigBlind: 10,
            MinBuyIn: 100,
            MaxBuyIn: 500,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit,
            Ante: 1);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Table.Should().NotBeNull();
        
        var config = result.Table!.Config;
        config.Should().NotBeNull();
        config.Variant.Should().Be(PokerVariant.FiveCardDraw);
        config.SmallBlind.Should().Be(5);
        config.BigBlind.Should().Be(10);
        config.MinBuyIn.Should().Be(100);
        config.MaxBuyIn.Should().Be(500);
        config.MaxSeats.Should().Be(6);
        config.LimitType.Should().Be(LimitType.FixedLimit);
        config.Ante.Should().Be(1);
    }

    #endregion

    #region Complete Flow Tests

    [Fact]
    public async Task FiveCardDrawFlow_CreateTable_JoinTable_LeaveTable()
    {
        // Step 1: Create a Five Card Draw table
        var createRequest = new CreateTableRequest(
            Name: "E2E Five Card Draw Flow Test",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 2,
            BigBlind: 4,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;
        createResult.Table!.OccupiedSeats.Should().Be(0);

        // Step 2: Join the table
        var joinRequest = new JoinTableRequest(tableId);
        var joinResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", joinRequest);
        joinResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var joinResult = await joinResponse.Content.ReadFromJsonAsync<JoinTableResponse>();
        joinResult!.Success.Should().BeTrue();
        joinResult.SeatNumber.Should().BeGreaterThan(0);

        // Step 3: Verify table state
        var tablesResponse = await _client.GetAsync("/api/tables");
        var tablesResult = await tablesResponse.Content.ReadFromJsonAsync<TablesListResponse>();
        var table = tablesResult!.Tables!.FirstOrDefault(t => t.Id == tableId);
        table.Should().NotBeNull();
        table!.OccupiedSeats.Should().Be(1);

        // Step 4: Leave the table
        var leaveRequest = new LeaveTableRequest(tableId, "TestPlayer");
        var leaveResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/leave", leaveRequest);
        leaveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: Verify table state after leaving
        tablesResponse = await _client.GetAsync("/api/tables");
        tablesResult = await tablesResponse.Content.ReadFromJsonAsync<TablesListResponse>();
        table = tablesResult!.Tables!.FirstOrDefault(t => t.Id == tableId);
        table.Should().NotBeNull();
        table!.OccupiedSeats.Should().Be(0);
    }

    [Fact]
    public async Task FiveCardDrawFlow_CreateTable_FillTable_JoinWaitingList()
    {
        // Step 1: Create a small Five Card Draw table
        var createRequest = new CreateTableRequest(
            Name: "Five Card Draw Waiting List Flow Test",
            Variant: PokerVariant.FiveCardDraw,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 20,
            MaxBuyIn: 100,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Step 2: Fill the table
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", new JoinTableRequest(tableId));
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", new JoinTableRequest(tableId));

        // Step 3: Verify table is full
        var tablesResponse = await _client.GetAsync("/api/tables");
        var tablesResult = await tablesResponse.Content.ReadFromJsonAsync<TablesListResponse>();
        var table = tablesResult!.Tables!.FirstOrDefault(t => t.Id == tableId);
        table!.OccupiedSeats.Should().Be(2);
        table.MaxSeats.Should().Be(2);

        // Step 4: Join waiting list
        var waitingListRequest = new JoinWaitingListRequest(tableId, "WaitingDrawPlayer");
        var waitingListResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", waitingListRequest);
        waitingListResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var waitingListResult = await waitingListResponse.Content.ReadFromJsonAsync<JoinWaitingListResponse>();
        waitingListResult!.Success.Should().BeTrue();
        waitingListResult.Entry!.Position.Should().Be(1);

        // Step 5: Verify waiting list
        var getWaitingListResponse = await _client.GetAsync($"/api/tables/{tableId}/waiting-list");
        var getWaitingListResult = await getWaitingListResponse.Content.ReadFromJsonAsync<GetWaitingListResponse>();
        getWaitingListResult!.Success.Should().BeTrue();
        getWaitingListResult.Entries.Should().HaveCount(1);
        getWaitingListResult.Entries![0].PlayerName.Should().Be("WaitingDrawPlayer");
    }

    #endregion

    #region RuleSet Validation Tests

    [Fact]
    public void PredefinedRuleSet_FiveCardDraw_HasCorrectConfiguration()
    {
        // Act
        var ruleSet = PredefinedRuleSets.FiveCardDraw;

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet.Variant.Should().Be(PokerVariant.FiveCardDraw);
        ruleSet.Name.Should().Contain("Five Card Draw");
        
        // Verify deck composition
        ruleSet.DeckComposition.DeckType.Should().Be(DeckType.Full52);
        ruleSet.DeckComposition.NumberOfDecks.Should().Be(1);

        // Verify hole card rules (5 cards, use all 5, allow draw up to 3)
        ruleSet.HoleCardRules.Count.Should().Be(5);
        ruleSet.HoleCardRules.MinUsedInHand.Should().Be(5);
        ruleSet.HoleCardRules.MaxUsedInHand.Should().Be(5);
        ruleSet.HoleCardRules.AllowDraw.Should().BeTrue();
        ruleSet.HoleCardRules.MaxDrawCount.Should().Be(3);

        // Verify no community cards (Draw games don't have community cards)
        ruleSet.CommunityCardRules.Should().BeNull();

        // Verify betting rounds (2 rounds: pre-draw and post-draw)
        ruleSet.BettingRounds.Should().HaveCount(2);
        ruleSet.BettingRounds[0].Name.Should().Be("Pre-Draw");
        ruleSet.BettingRounds[1].Name.Should().Be("Post-Draw");

        // Verify ante (Draw uses ante, not blinds)
        ruleSet.AnteBlindRules.Should().NotBeNull();
        ruleSet.AnteBlindRules!.HasAnte.Should().BeTrue();
        ruleSet.AnteBlindRules.HasSmallBlind.Should().BeFalse();
        ruleSet.AnteBlindRules.HasBigBlind.Should().BeFalse();

        // Verify fixed limit (most common for Draw)
        ruleSet.LimitType.Should().Be(LimitType.FixedLimit);

        // Verify showdown rules
        ruleSet.ShowdownRules.Should().NotBeNull();
        ruleSet.ShowdownRules!.ShowOrder.Should().Be(ShowdownOrder.LastAggressor);
        ruleSet.ShowdownRules.AllowMuck.Should().BeTrue();
        ruleSet.ShowdownRules.ShowAllOnAllIn.Should().BeTrue();

        // Verify no wildcards
        ruleSet.WildcardRules.Should().BeNull();

        // Verify not hi-lo (basic Five Card Draw is high only)
        ruleSet.HiLoRules.Should().BeNull();

        // Verify card visibility (all cards private)
        ruleSet.CardVisibility.HoleCardsPrivate.Should().BeTrue();
        ruleSet.CardVisibility.CommunityCardsPublic.Should().BeFalse();

        // Verify special rules (draw phase)
        ruleSet.SpecialRules.Should().NotBeNull();
        ruleSet.SpecialRules.Should().Contain(r => r.Id == "draw-phase" && r.Enabled);
    }

    [Fact]
    public void PredefinedRuleSet_FiveCardDraw_IsAvailableByVariant()
    {
        // Act
        var ruleSet = PredefinedRuleSets.GetByVariant(PokerVariant.FiveCardDraw);

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet!.Variant.Should().Be(PokerVariant.FiveCardDraw);
    }

    [Fact]
    public void PredefinedRuleSets_All_IncludesFiveCardDraw()
    {
        // Act
        var allRuleSets = PredefinedRuleSets.All;

        // Assert
        allRuleSets.Should().ContainKey(PokerVariant.FiveCardDraw);
        allRuleSets[PokerVariant.FiveCardDraw].Should().Be(PredefinedRuleSets.FiveCardDraw);
    }

    #endregion
}
