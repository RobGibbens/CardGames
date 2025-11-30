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
/// End-to-end API tests for Seven Card Stud integration.
/// These tests verify the complete API flow for Seven Card Stud game management.
/// </summary>
public class SevenCardStudApiEndToEndTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string SevenCardStudVariantId = "seven-card-stud";
    
    private readonly HttpClient _client;

    public SevenCardStudApiEndToEndTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    #region Variant Discovery Tests

    [Fact]
    public async Task GetVariants_IncludesSevenCardStudWithCompleteInfo()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        
        var sevenCardStud = result.Variants!.FirstOrDefault(v => v.Id == SevenCardStudVariantId);
        sevenCardStud.Should().NotBeNull();
        sevenCardStud!.Name.Should().Be("Seven Card Stud");
        sevenCardStud.Description.Should().NotBeNullOrEmpty();
        sevenCardStud.Description.Should().Contain("7 cards");
        sevenCardStud.MinPlayers.Should().Be(2);
        sevenCardStud.MaxPlayers.Should().Be(7);
    }

    [Fact]
    public async Task GetVariantById_SevenCardStud_ReturnsDetailedInfo()
    {
        // Act
        var response = await _client.GetAsync($"/api/variants/{SevenCardStudVariantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be(SevenCardStudVariantId);
        result.Variant.Name.Should().Be("Seven Card Stud");
    }

    [Fact]
    public async Task GetVariantById_SevenCardStud_CaseInsensitive()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/SEVEN-CARD-STUD");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant!.Id.Should().Be(SevenCardStudVariantId);
    }

    #endregion

    #region Table Creation Tests for Seven Card Stud

    [Fact]
    public async Task CreateTable_SevenCardStud_FixedLimit_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Seven Card Stud Table",
            Variant: PokerVariant.SevenCardStud,
            SmallBlind: 5,
            BigBlind: 10,
            MinBuyIn: 100,
            MaxBuyIn: 500,
            MaxSeats: 7,
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
        result.Table!.Variant.Should().Be(PokerVariant.SevenCardStud);
        result.Table.LimitType.Should().Be(LimitType.FixedLimit);
        result.Table.State.Should().Be(GameState.WaitingForPlayers);
    }

    [Fact]
    public async Task CreateTable_SevenCardStud_WithAnte_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Seven Card Stud with Ante",
            Variant: PokerVariant.SevenCardStud,
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
    public async Task CreateTable_SevenCardStud_HeadsUp_Succeeds()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Heads Up Seven Card Stud",
            Variant: PokerVariant.SevenCardStud,
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
    public async Task CreateTable_SevenCardStud_MaxPlayers_Succeeds()
    {
        // Arrange - Seven Card Stud supports max 7 players due to card constraints
        var request = new CreateTableRequest(
            Name: "Full Ring Seven Card Stud",
            Variant: PokerVariant.SevenCardStud,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 20,
            MaxBuyIn: 100,
            MaxSeats: 7,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.FixedLimit);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.MaxSeats.Should().Be(7);
    }

    [Fact]
    public async Task CreateTable_SevenCardStud_TooManyPlayers_Fails()
    {
        // Arrange - Seven Card Stud max is 7 players (7 * 7 = 49 cards), 8+ exceeds deck (8 * 7 = 56 > 52)
        var request = new CreateTableRequest(
            Name: "Too Many Players Table",
            Variant: PokerVariant.SevenCardStud,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 20,
            MaxBuyIn: 100,
            MaxSeats: 9,
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

    #region Table Filtering Tests for Seven Card Stud

    [Fact]
    public async Task GetTables_FilterBySevenCardStud_ReturnsOnlyStudTables()
    {
        // Act
        var response = await _client.GetAsync($"/api/tables?variant={PokerVariant.SevenCardStud}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => t.Variant == PokerVariant.SevenCardStud);
    }

    [Fact]
    public async Task GetTables_FilterByStudAndStakes_ReturnsFilteredTables()
    {
        // Act
        var response = await _client.GetAsync($"/api/tables?variant={PokerVariant.SevenCardStud}&minSmallBlind=1&maxSmallBlind=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().OnlyContain(t => 
            t.Variant == PokerVariant.SevenCardStud &&
            t.SmallBlind >= 1 &&
            t.SmallBlind <= 10);
    }

    #endregion

    #region Quick Join Tests for Seven Card Stud

    [Fact]
    public async Task QuickJoin_FilterBySevenCardStud_JoinsStudTable()
    {
        // Act
        var request = new QuickJoinRequest(Variant: PokerVariant.SevenCardStud);
        var response = await _client.PostAsJsonAsync("/api/tables/quick-join", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.Variant.Should().Be(PokerVariant.SevenCardStud);
    }

    #endregion

    #region Table Configuration Tests

    [Fact]
    public async Task CreateTable_SevenCardStud_HasCorrectConfig()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Config Verification Table",
            Variant: PokerVariant.SevenCardStud,
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
        config.Variant.Should().Be(PokerVariant.SevenCardStud);
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
    public async Task SevenCardStudFlow_CreateTable_JoinTable_LeaveTable()
    {
        // Step 1: Create a Seven Card Stud table
        var createRequest = new CreateTableRequest(
            Name: "E2E Seven Card Stud Flow Test",
            Variant: PokerVariant.SevenCardStud,
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
    public async Task SevenCardStudFlow_CreateTable_FillTable_JoinWaitingList()
    {
        // Step 1: Create a small Seven Card Stud table
        var createRequest = new CreateTableRequest(
            Name: "Seven Card Stud Waiting List Flow Test",
            Variant: PokerVariant.SevenCardStud,
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
        var waitingListRequest = new JoinWaitingListRequest(tableId, "WaitingStudPlayer");
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
        getWaitingListResult.Entries![0].PlayerName.Should().Be("WaitingStudPlayer");
    }

    #endregion

    #region RuleSet Validation Tests

    [Fact]
    public void PredefinedRuleSet_SevenCardStud_HasCorrectConfiguration()
    {
        // Act
        var ruleSet = PredefinedRuleSets.SevenCardStud;

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet.Variant.Should().Be(PokerVariant.SevenCardStud);
        ruleSet.Name.Should().Contain("Seven Card Stud");
        
        // Verify deck composition
        ruleSet.DeckComposition.DeckType.Should().Be(DeckType.Full52);
        ruleSet.DeckComposition.NumberOfDecks.Should().Be(1);

        // Verify hole card rules (7 cards total, use best 5)
        ruleSet.HoleCardRules.Count.Should().Be(7);
        ruleSet.HoleCardRules.MinUsedInHand.Should().Be(5);
        ruleSet.HoleCardRules.MaxUsedInHand.Should().Be(5);
        ruleSet.HoleCardRules.AllowDraw.Should().BeFalse();

        // Verify no community cards (Stud uses individual cards, not community cards)
        ruleSet.CommunityCardRules.Should().BeNull();

        // Verify betting rounds (5 streets)
        ruleSet.BettingRounds.Should().HaveCount(5);
        ruleSet.BettingRounds[0].Name.Should().Be("Third Street");
        ruleSet.BettingRounds[1].Name.Should().Be("Fourth Street");
        ruleSet.BettingRounds[2].Name.Should().Be("Fifth Street");
        ruleSet.BettingRounds[3].Name.Should().Be("Sixth Street");
        ruleSet.BettingRounds[4].Name.Should().Be("Seventh Street");

        // Verify ante (Stud uses ante, not blinds)
        ruleSet.AnteBlindRules.Should().NotBeNull();
        ruleSet.AnteBlindRules!.HasAnte.Should().BeTrue();
        ruleSet.AnteBlindRules.HasSmallBlind.Should().BeFalse();
        ruleSet.AnteBlindRules.HasBigBlind.Should().BeFalse();

        // Verify fixed limit (most common for Stud)
        ruleSet.LimitType.Should().Be(LimitType.FixedLimit);

        // Verify showdown rules
        ruleSet.ShowdownRules.Should().NotBeNull();
        ruleSet.ShowdownRules!.ShowOrder.Should().Be(ShowdownOrder.LastAggressor);
        ruleSet.ShowdownRules.AllowMuck.Should().BeTrue();
        ruleSet.ShowdownRules.ShowAllOnAllIn.Should().BeTrue();

        // Verify no wildcards
        ruleSet.WildcardRules.Should().BeNull();

        // Verify not hi-lo (basic Seven Card Stud is high only)
        ruleSet.HiLoRules.Should().BeNull();

        // Verify card visibility (face-down and face-up indices)
        ruleSet.CardVisibility.FaceDownIndices.Should().NotBeNull();
        ruleSet.CardVisibility.FaceUpIndices.Should().NotBeNull();
        ruleSet.CardVisibility.FaceDownIndices.Should().Contain(new[] { 0, 1, 6 }); // First 2 and last card face down
        ruleSet.CardVisibility.FaceUpIndices.Should().Contain(new[] { 2, 3, 4, 5 }); // Cards 3-6 face up

        // Verify special rules (bring-in)
        ruleSet.SpecialRules.Should().NotBeNull();
        ruleSet.SpecialRules.Should().Contain(r => r.Id == "bring-in" && r.Enabled);
    }

    [Fact]
    public void PredefinedRuleSet_SevenCardStud_IsAvailableByVariant()
    {
        // Act
        var ruleSet = PredefinedRuleSets.GetByVariant(PokerVariant.SevenCardStud);

        // Assert
        ruleSet.Should().NotBeNull();
        ruleSet!.Variant.Should().Be(PokerVariant.SevenCardStud);
    }

    [Fact]
    public void PredefinedRuleSets_All_IncludesSevenCardStud()
    {
        // Act
        var allRuleSets = PredefinedRuleSets.All;

        // Assert
        allRuleSets.Should().ContainKey(PokerVariant.SevenCardStud);
        allRuleSets[PokerVariant.SevenCardStud].Should().Be(PredefinedRuleSets.SevenCardStud);
    }

    #endregion
}
