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
/// End-to-end API tests for Omaha integration.
/// These tests verify the complete API flow for Omaha game management,
/// including variant discovery, table creation, filtering, and ruleset validation.
/// </summary>
public class OmahaApiEndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string OmahaVariantId = "omaha";
    
    private readonly HttpClient _client;

    public OmahaApiEndToEndTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    #region Variant Discovery Tests

    [Fact]
    public async Task GetVariants_IncludesOmahaWithCompleteInfo()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        
        var omaha = result.Variants!.FirstOrDefault(v => v.Id == OmahaVariantId);
        omaha.Should().NotBeNull();
        omaha!.Name.Should().Be("Omaha");
        omaha.Description.Should().NotBeNullOrEmpty();
        omaha.Description.Should().Contain("4 hole cards");
        omaha.MinPlayers.Should().Be(2);
        omaha.MaxPlayers.Should().Be(10);
    }

    [Fact]
    public async Task GetVariantById_Omaha_ReturnsDetailedInfo()
    {
        // Act
        var response = await _client.GetAsync($"/api/variants/{OmahaVariantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be(OmahaVariantId);
        result.Variant.Name.Should().Be("Omaha");
    }

    #endregion

    #region Table Creation Tests for Omaha

    [Fact]
    public async Task CreateTable_Omaha_PotLimit_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "PLO Table",
            Variant: PokerVariant.Omaha,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.PotLimit);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.Variant.Should().Be(PokerVariant.Omaha);
        result.Table.LimitType.Should().Be(LimitType.PotLimit);
        result.Table.State.Should().Be(GameState.WaitingForPlayers);
    }

    [Fact]
    public async Task CreateTable_Omaha_NoLimit_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "NL Omaha Table",
            Variant: PokerVariant.Omaha,
            SmallBlind: 2,
            BigBlind: 5,
            MinBuyIn: 100,
            MaxBuyIn: 500,
            MaxSeats: 9,
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
    public async Task CreateTable_Omaha_FixedLimit_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Fixed Limit Omaha",
            Variant: PokerVariant.Omaha,
            SmallBlind: 2,
            BigBlind: 4,
            MinBuyIn: 80,
            MaxBuyIn: 400,
            MaxSeats: 9,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.LimitType.Should().Be(LimitType.FixedLimit);
    }

    [Fact]
    public async Task CreateTable_Omaha_WithAnte_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Omaha with Ante",
            Variant: PokerVariant.Omaha,
            SmallBlind: 5,
            BigBlind: 10,
            MinBuyIn: 200,
            MaxBuyIn: 1000,
            MaxSeats: 9,
            Privacy: TablePrivacy.Public,
            Ante: 2);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.Ante.Should().Be(2);
    }

    [Fact]
    public async Task CreateTable_Omaha_HeadsUp_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Heads Up Omaha",
            Variant: PokerVariant.Omaha,
            SmallBlind: 10,
            BigBlind: 20,
            MinBuyIn: 400,
            MaxBuyIn: 2000,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

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
    public async Task CreateTable_Omaha_FullRing_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Full Ring Omaha",
            Variant: PokerVariant.Omaha,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 10,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.MaxSeats.Should().Be(10);
    }

    #endregion

    #region Table Filtering Tests for Omaha

    [Fact]
    public async Task GetTables_FilterByOmaha_ReturnsOnlyOmahaTables()
    {
        // Act
        var response = await _client.GetAsync($"/api/tables?variant={PokerVariant.Omaha}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => t.Variant == PokerVariant.Omaha);
    }

    [Fact]
    public async Task GetTables_FilterByOmahaAndStakes_ReturnsFilteredTables()
    {
        // Act
        var response = await _client.GetAsync($"/api/tables?variant={PokerVariant.Omaha}&minSmallBlind=1&maxSmallBlind=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().OnlyContain(t => 
            t.Variant == PokerVariant.Omaha &&
            t.SmallBlind >= 1 &&
            t.SmallBlind <= 10);
    }

    #endregion

    #region Quick Join Tests for Omaha

    [Fact]
    public async Task QuickJoin_FilterByOmaha_JoinsOmahaTable()
    {
        // Act
        var request = new QuickJoinRequest(Variant: PokerVariant.Omaha);
        var response = await _client.PostAsJsonAsync("/api/tables/quick-join", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.Variant.Should().Be(PokerVariant.Omaha);
    }

    #endregion

    #region Table Configuration Tests

    [Fact]
    public async Task CreateTable_Omaha_HasCorrectConfig()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Omaha Config Verification",
            Variant: PokerVariant.Omaha,
            SmallBlind: 5,
            BigBlind: 10,
            MinBuyIn: 200,
            MaxBuyIn: 1000,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.PotLimit,
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
        config.Variant.Should().Be(PokerVariant.Omaha);
        config.SmallBlind.Should().Be(5);
        config.BigBlind.Should().Be(10);
        config.MinBuyIn.Should().Be(200);
        config.MaxBuyIn.Should().Be(1000);
        config.MaxSeats.Should().Be(6);
        config.LimitType.Should().Be(LimitType.PotLimit);
        config.Ante.Should().Be(1);
    }

    #endregion

    #region Complete Flow Tests

    [Fact]
    public async Task OmahaFlow_CreateTable_JoinTable_LeaveTable()
    {
        // Step 1: Create an Omaha table
        var createRequest = new CreateTableRequest(
            Name: "E2E Omaha Flow Test",
            Variant: PokerVariant.Omaha,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

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
    public async Task OmahaFlow_CreateTable_FillTable_JoinWaitingList()
    {
        // Step 1: Create a small Omaha table
        var createRequest = new CreateTableRequest(
            Name: "Omaha Waiting List Flow Test",
            Variant: PokerVariant.Omaha,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

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
        var waitingListRequest = new JoinWaitingListRequest(tableId, "OmahaWaitingPlayer");
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
        getWaitingListResult.Entries![0].PlayerName.Should().Be("OmahaWaitingPlayer");
    }

    #endregion

    #region RuleSet Validation Tests

    [Fact]
    public void PredefinedRuleSet_Omaha_HasCorrectConfiguration()
    {
        // Act
        var ruleSet = PredefinedRuleSets.Omaha;

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet.Variant.Should().Be(PokerVariant.Omaha);
        ruleSet.Name.Should().Contain("Omaha");
        
        // Verify deck composition
        ruleSet.DeckComposition.DeckType.Should().Be(DeckType.Full52);
        ruleSet.DeckComposition.NumberOfDecks.Should().Be(1);

        // Verify hole card rules - Omaha has 4 hole cards, must use exactly 2
        ruleSet.HoleCardRules.Count.Should().Be(4);
        ruleSet.HoleCardRules.MinUsedInHand.Should().Be(2);
        ruleSet.HoleCardRules.MaxUsedInHand.Should().Be(2);
        ruleSet.HoleCardRules.AllowDraw.Should().BeFalse();

        // Verify community card rules - must use exactly 3 community cards
        ruleSet.CommunityCardRules.Should().NotBeNull();
        ruleSet.CommunityCardRules!.TotalCount.Should().Be(5);
        ruleSet.CommunityCardRules.MinUsedInHand.Should().Be(3);
        ruleSet.CommunityCardRules.MaxUsedInHand.Should().Be(3);

        // Verify betting rounds
        ruleSet.BettingRounds.Should().HaveCount(4);
        ruleSet.BettingRounds[0].Name.Should().Be("Preflop");
        ruleSet.BettingRounds[1].Name.Should().Be("Flop");
        ruleSet.BettingRounds[2].Name.Should().Be("Turn");
        ruleSet.BettingRounds[3].Name.Should().Be("River");

        // Verify blinds
        ruleSet.AnteBlindRules.Should().NotBeNull();
        ruleSet.AnteBlindRules!.HasSmallBlind.Should().BeTrue();
        ruleSet.AnteBlindRules.HasBigBlind.Should().BeTrue();
        ruleSet.AnteBlindRules.HasAnte.Should().BeFalse();

        // Verify showdown rules
        ruleSet.ShowdownRules.Should().NotBeNull();
        ruleSet.ShowdownRules!.ShowOrder.Should().Be(ShowdownOrder.LastAggressor);
        ruleSet.ShowdownRules.AllowMuck.Should().BeTrue();
        ruleSet.ShowdownRules.ShowAllOnAllIn.Should().BeTrue();

        // Verify no wildcards
        ruleSet.WildcardRules.Should().BeNull();

        // Verify not hi-lo (standard Omaha high only)
        ruleSet.HiLoRules.Should().BeNull();
    }

    #endregion
}
