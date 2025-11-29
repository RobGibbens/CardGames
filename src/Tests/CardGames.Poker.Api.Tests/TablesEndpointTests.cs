using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Lobby;
using CardGames.Poker.Shared.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class TablesEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TablesEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTables_WithNoFilters_ReturnsAllTables()
    {
        // Act
        var response = await _client.GetAsync("/api/tables");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetTables_FilterByVariant_ReturnsOnlyMatchingTables()
    {
        // Act
        var response = await _client.GetAsync($"/api/tables?variant={PokerVariant.TexasHoldem}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => t.Variant == PokerVariant.TexasHoldem);
    }

    [Fact]
    public async Task GetTables_FilterByMinSmallBlind_ReturnsTablesWithHigherStakes()
    {
        // Act
        var response = await _client.GetAsync("/api/tables?minSmallBlind=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => t.SmallBlind >= 5);
    }

    [Fact]
    public async Task GetTables_FilterByMaxSmallBlind_ReturnsTablesWithLowerStakes()
    {
        // Act
        var response = await _client.GetAsync("/api/tables?maxSmallBlind=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => t.SmallBlind <= 2);
    }

    [Fact]
    public async Task GetTables_FilterByMinAvailableSeats_ReturnsTablesWithEnoughSeats()
    {
        // Act
        var response = await _client.GetAsync("/api/tables?minAvailableSeats=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => (t.MaxSeats - t.OccupiedSeats) >= 2);
    }

    [Fact]
    public async Task GetTables_HideFullTables_ExcludesFullTables()
    {
        // Act
        var response = await _client.GetAsync("/api/tables?hideFullTables=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => t.OccupiedSeats < t.MaxSeats);
    }

    [Fact]
    public async Task GetTables_HideEmptyTables_ExcludesEmptyTables()
    {
        // Act
        var response = await _client.GetAsync("/api/tables?hideEmptyTables=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => t.OccupiedSeats > 0);
    }

    [Fact]
    public async Task GetTables_CombinedFilters_ReturnsFilteredResults()
    {
        // Act
        var response = await _client.GetAsync($"/api/tables?variant={PokerVariant.TexasHoldem}&minSmallBlind=1&maxSmallBlind=10&hideFullTables=true");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Tables.Should().NotBeNull();
        result.Tables!.Should().OnlyContain(t => 
            t.Variant == PokerVariant.TexasHoldem &&
            t.SmallBlind >= 1 &&
            t.SmallBlind <= 10 &&
            t.OccupiedSeats < t.MaxSeats);
    }

    [Fact]
    public async Task GetTables_ReturnsCorrectTableProperties()
    {
        // Act
        var response = await _client.GetAsync("/api/tables");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        result!.Tables.Should().NotBeNull();
        
        var table = result.Tables!.First();
        table.Id.Should().NotBeEmpty();
        table.Name.Should().NotBeNullOrEmpty();
        table.SmallBlind.Should().BeGreaterThan(0);
        table.BigBlind.Should().BeGreaterThanOrEqualTo(table.SmallBlind);
        table.MinBuyIn.Should().BeGreaterThan(0);
        table.MaxBuyIn.Should().BeGreaterThanOrEqualTo(table.MinBuyIn);
        table.MaxSeats.Should().BeGreaterThan(0);
        table.OccupiedSeats.Should().BeGreaterThanOrEqualTo(0);
        table.OccupiedSeats.Should().BeLessThanOrEqualTo(table.MaxSeats);
    }

    [Fact]
    public async Task CreateTable_WithValidRequest_ReturnsCreatedTable()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.TableId.Should().NotBeEmpty();
        result.JoinLink.Should().NotBeNullOrEmpty();
        result.JoinLink.Should().Contain(result.TableId.ToString());
        result.Table.Should().NotBeNull();
        result.Table!.Name.Should().Be("Test Table");
        result.Table.Variant.Should().Be(PokerVariant.TexasHoldem);
        result.Table.SmallBlind.Should().Be(1);
        result.Table.BigBlind.Should().Be(2);
        result.Table.MinBuyIn.Should().Be(40);
        result.Table.MaxBuyIn.Should().Be(200);
        result.Table.MaxSeats.Should().Be(6);
        result.Table.Privacy.Should().Be(TablePrivacy.Public);
        result.Table.OccupiedSeats.Should().Be(0);
        result.Table.State.Should().Be(GameState.WaitingForPlayers);
    }

    [Fact]
    public async Task CreateTable_WithPrivateTable_ReturnsCreatedTable()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Private Test Table",
            Variant: PokerVariant.Omaha,
            SmallBlind: 5,
            BigBlind: 10,
            MinBuyIn: 100,
            MaxBuyIn: 500,
            MaxSeats: 9,
            Privacy: TablePrivacy.Private);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.Privacy.Should().Be(TablePrivacy.Private);
    }

    [Fact]
    public async Task CreateTable_WithPasswordProtectedTable_ReturnsCreatedTable()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Password Test Table",
            Variant: PokerVariant.SevenCardStud,
            SmallBlind: 2,
            BigBlind: 4,
            MinBuyIn: 80,
            MaxBuyIn: 400,
            MaxSeats: 8,
            Privacy: TablePrivacy.Password,
            Password: "secret123");

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.Privacy.Should().Be(TablePrivacy.Password);
    }

    [Fact]
    public async Task CreateTable_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public async Task CreateTable_WithNameTooLong_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: new string('a', 51),
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("50");
    }

    [Fact]
    public async Task CreateTable_WithInvalidSmallBlind_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 0,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Small blind");
    }

    [Fact]
    public async Task CreateTable_WithBigBlindLessThanSmallBlind_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 5,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Big blind");
    }

    [Fact]
    public async Task CreateTable_WithInvalidMinBuyIn_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 0,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Minimum buy-in");
    }

    [Fact]
    public async Task CreateTable_WithMaxBuyInLessThanMinBuyIn_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 200,
            MaxBuyIn: 100,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Maximum buy-in");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(11)]
    public async Task CreateTable_WithInvalidSeatCount_ReturnsBadRequest(int seatCount)
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: seatCount,
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("seats");
    }

    [Fact]
    public async Task CreateTable_PasswordProtectedWithoutPassword_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Password,
            Password: null);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Password is required");
    }

    [Fact]
    public async Task JoinTable_PublicTable_Succeeds()
    {
        // Arrange - First create a public table
        var createRequest = new CreateTableRequest(
            Name: "Join Test Public Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Act
        var joinRequest = new JoinTableRequest(tableId);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", joinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.TableId.Should().Be(tableId);
        result.SeatNumber.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task JoinTable_PasswordProtectedTable_WithCorrectPassword_Succeeds()
    {
        // Arrange - First create a password-protected table
        var createRequest = new CreateTableRequest(
            Name: "Join Test Password Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Password,
            Password: "testpass123");

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Act
        var joinRequest = new JoinTableRequest(tableId, Password: "testpass123");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", joinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.TableId.Should().Be(tableId);
        result.SeatNumber.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task JoinTable_PasswordProtectedTable_WithWrongPassword_Fails()
    {
        // Arrange - First create a password-protected table
        var createRequest = new CreateTableRequest(
            Name: "Join Test Wrong Password Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Password,
            Password: "correctPassword");

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Act
        var joinRequest = new JoinTableRequest(tableId, Password: "wrongPassword");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", joinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Invalid password");
    }

    [Fact]
    public async Task JoinTable_PasswordProtectedTable_WithoutPassword_Fails()
    {
        // Arrange - First create a password-protected table
        var createRequest = new CreateTableRequest(
            Name: "Join Test No Password Provided Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Password,
            Password: "secretPass");

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Act
        var joinRequest = new JoinTableRequest(tableId); // No password provided
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", joinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Password is required");
    }

    [Fact]
    public async Task JoinTable_NonExistentTable_Fails()
    {
        // Arrange
        var nonExistentTableId = Guid.NewGuid();
        var joinRequest = new JoinTableRequest(nonExistentTableId);

        // Act
        var response = await _client.PostAsJsonAsync($"/api/tables/{nonExistentTableId}/join", joinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task JoinTable_MismatchedTableId_ReturnsBadRequest()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Mismatch Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Act - Send request with mismatched table ID
        var differentTableId = Guid.NewGuid();
        var joinRequest = new JoinTableRequest(differentTableId);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", joinRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<JoinTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("does not match");
    }

    [Fact]
    public async Task QuickJoin_WithAvailableTables_ReturnsTable()
    {
        // Act
        var request = new QuickJoinRequest();
        var response = await _client.PostAsJsonAsync("/api/tables/quick-join", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.TableId.Should().NotBeEmpty();
        result.SeatNumber.Should().BeGreaterThan(0);
        result.Table.Should().NotBeNull();
        result.Table!.Privacy.Should().Be(TablePrivacy.Public);
    }

    [Fact]
    public async Task QuickJoin_WithVariantFilter_ReturnsMatchingTable()
    {
        // Act
        var request = new QuickJoinRequest(Variant: PokerVariant.TexasHoldem);
        var response = await _client.PostAsJsonAsync("/api/tables/quick-join", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.Variant.Should().Be(PokerVariant.TexasHoldem);
    }

    [Fact]
    public async Task QuickJoin_WithStakesFilter_ReturnsMatchingTable()
    {
        // Act
        var request = new QuickJoinRequest(MinSmallBlind: 1, MaxSmallBlind: 5);
        var response = await _client.PostAsJsonAsync("/api/tables/quick-join", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.SmallBlind.Should().BeGreaterThanOrEqualTo(1);
        result.Table.SmallBlind.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task QuickJoin_DoesNotJoinPasswordProtectedTables()
    {
        // Act - Quick join should only join public tables
        var request = new QuickJoinRequest();
        var response = await _client.PostAsJsonAsync("/api/tables/quick-join", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<QuickJoinResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.Privacy.Should().Be(TablePrivacy.Public);
    }

    [Fact]
    public async Task JoinWaitingList_WithValidRequest_Succeeds()
    {
        // Arrange - Create a full table
        var createRequest = new CreateTableRequest(
            Name: "Full Table Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Fill up the table
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", new JoinTableRequest(tableId));
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", new JoinTableRequest(tableId));

        // Act - Join waiting list
        var waitingListRequest = new JoinWaitingListRequest(tableId, "TestPlayer");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", waitingListRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<JoinWaitingListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Entry.Should().NotBeNull();
        result.Entry!.PlayerName.Should().Be("TestPlayer");
        result.Entry.Position.Should().Be(1);
        result.Entry.TableId.Should().Be(tableId);
    }

    [Fact]
    public async Task JoinWaitingList_WithInvalidPlayerName_ReturnsBadRequest()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Waiting List Invalid Player Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Act - Join waiting list with empty player name
        var waitingListRequest = new JoinWaitingListRequest(tableId, "");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", waitingListRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<JoinWaitingListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Player name is required");
    }

    [Fact]
    public async Task JoinWaitingList_DuplicatePlayer_ReturnsBadRequest()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Waiting List Duplicate Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Join waiting list first time
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "TestPlayer"));

        // Act - Try to join again
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "TestPlayer"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<JoinWaitingListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("already on the waiting list");
    }

    [Fact]
    public async Task GetWaitingList_ReturnsCorrectEntries()
    {
        // Arrange - Create a table and add players to waiting list
        var createRequest = new CreateTableRequest(
            Name: "Get Waiting List Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Add players to waiting list
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "Player1"));
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "Player2"));

        // Act
        var response = await _client.GetAsync($"/api/tables/{tableId}/waiting-list");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetWaitingListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Entries.Should().NotBeNull();
        result.Entries!.Count.Should().Be(2);
        result.Entries.Should().Contain(e => e.PlayerName == "Player1" && e.Position == 1);
        result.Entries.Should().Contain(e => e.PlayerName == "Player2" && e.Position == 2);
    }

    [Fact]
    public async Task GetWaitingList_NonExistentTable_ReturnsNotFound()
    {
        // Arrange
        var nonExistentTableId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/tables/{nonExistentTableId}/waiting-list");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<GetWaitingListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task LeaveWaitingList_Succeeds()
    {
        // Arrange - Create a table and join waiting list
        var createRequest = new CreateTableRequest(
            Name: "Leave Waiting List Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "TestPlayer"));

        // Act
        var response = await _client.DeleteAsync($"/api/tables/{tableId}/waiting-list/TestPlayer");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LeaveWaitingListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LeaveWaitingList_UpdatesPositions()
    {
        // Arrange - Create a table and add multiple players to waiting list
        var createRequest = new CreateTableRequest(
            Name: "Leave Waiting List Position Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "Player1"));
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "Player2"));
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "Player3"));

        // Act - Remove player 1
        await _client.DeleteAsync($"/api/tables/{tableId}/waiting-list/Player1");

        // Assert - Check positions are updated
        var response = await _client.GetAsync($"/api/tables/{tableId}/waiting-list");
        var result = await response.Content.ReadFromJsonAsync<GetWaitingListResponse>();
        result.Should().NotBeNull();
        result!.Entries.Should().HaveCount(2);
        result.Entries!.Should().Contain(e => e.PlayerName == "Player2" && e.Position == 1);
        result.Entries.Should().Contain(e => e.PlayerName == "Player3" && e.Position == 2);
    }

    [Fact]
    public async Task LeaveWaitingList_PlayerNotOnList_ReturnsBadRequest()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Leave Waiting List Not Found Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Act - Try to leave waiting list without being on it
        var response = await _client.DeleteAsync($"/api/tables/{tableId}/waiting-list/NonExistentPlayer");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<LeaveWaitingListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not on the waiting list");
    }

    [Fact]
    public async Task LeaveTable_Succeeds()
    {
        // Arrange - Create a table and join
        var createRequest = new CreateTableRequest(
            Name: "Leave Table Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        await _client.PostAsJsonAsync($"/api/tables/{tableId}/join", new JoinTableRequest(tableId));

        // Act
        var leaveRequest = new LeaveTableRequest(tableId, "TestPlayer");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/leave", leaveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LeaveTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LeaveTable_EmptyTable_ReturnsBadRequest()
    {
        // Arrange - Create a table without joining
        var createRequest = new CreateTableRequest(
            Name: "Leave Empty Table Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Act - Try to leave an empty table
        var leaveRequest = new LeaveTableRequest(tableId, "TestPlayer");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/leave", leaveRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<LeaveTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("no players");
    }

    [Fact]
    public async Task TableSummary_IncludesWaitingListCount()
    {
        // Arrange - Create a table and add players to waiting list
        var createRequest = new CreateTableRequest(
            Name: "Waiting List Count Test",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 2,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Add players to waiting list
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "Player1"));
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/waiting-list", new JoinWaitingListRequest(tableId, "Player2"));

        // Act - Get tables list
        var response = await _client.GetAsync("/api/tables");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TablesListResponse>();
        result.Should().NotBeNull();
        var table = result!.Tables!.FirstOrDefault(t => t.Id == tableId);
        table.Should().NotBeNull();
        table!.WaitingListCount.Should().Be(2);
    }

    [Fact]
    public async Task CreateTable_WithLimitType_ReturnsTableWithCorrectLimitType()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Pot Limit Table",
            Variant: PokerVariant.Omaha,
            SmallBlind: 2,
            BigBlind: 4,
            MinBuyIn: 80,
            MaxBuyIn: 400,
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
        result.Table!.LimitType.Should().Be(LimitType.PotLimit);
    }

    [Fact]
    public async Task CreateTable_WithAnte_ReturnsTableWithCorrectAnte()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Ante Table",
            Variant: PokerVariant.SevenCardStud,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 8,
            Privacy: TablePrivacy.Public,
            Ante: 1);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        result.Table!.Ante.Should().Be(1);
    }

    [Fact]
    public async Task CreateTable_WithNegativeAnte_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Invalid Ante Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            Ante: -1);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Ante");
    }

    [Fact]
    public async Task CreateTable_SevenCardStudWithTooManySeats_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Invalid Stud Table",
            Variant: PokerVariant.SevenCardStud,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 9, // Seven Card Stud max is 8
            Privacy: TablePrivacy.Public);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("Seven Card Stud");
    }

    [Fact]
    public async Task CreateTable_TableResponseIncludesConfig()
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: "Config Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 5,
            BigBlind: 10,
            MinBuyIn: 200,
            MaxBuyIn: 1000,
            MaxSeats: 9,
            Privacy: TablePrivacy.Public,
            LimitType: LimitType.NoLimit,
            Ante: 2);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table.Should().NotBeNull();
        
        var config = result.Table!.Config;
        config.Should().NotBeNull();
        config.Variant.Should().Be(PokerVariant.TexasHoldem);
        config.MaxSeats.Should().Be(9);
        config.SmallBlind.Should().Be(5);
        config.BigBlind.Should().Be(10);
        config.LimitType.Should().Be(LimitType.NoLimit);
        config.MinBuyIn.Should().Be(200);
        config.MaxBuyIn.Should().Be(1000);
        config.Ante.Should().Be(2);
    }

    [Theory]
    [InlineData(LimitType.NoLimit)]
    [InlineData(LimitType.FixedLimit)]
    [InlineData(LimitType.PotLimit)]
    [InlineData(LimitType.SpreadLimit)]
    public async Task CreateTable_AllLimitTypes_AreAccepted(LimitType limitType)
    {
        // Arrange
        var request = new CreateTableRequest(
            Name: $"Table with {limitType}",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 1,
            BigBlind: 2,
            MinBuyIn: 40,
            MaxBuyIn: 200,
            MaxSeats: 6,
            Privacy: TablePrivacy.Public,
            LimitType: limitType);

        // Act
        var response = await _client.PostAsJsonAsync("/api/tables", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<CreateTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Table!.LimitType.Should().Be(limitType);
    }
}
