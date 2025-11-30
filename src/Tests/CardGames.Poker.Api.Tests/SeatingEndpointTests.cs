using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Lobby;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class SeatingEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SeatingEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    #region GetSeats Tests

    [Fact]
    public async Task GetSeats_ValidTable_ReturnsSeats()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Seats Test Table",
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
        var response = await _client.GetAsync($"/api/tables/{tableId}/seats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<GetSeatsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Seats.Should().NotBeNull();
        result.Seats!.Count.Should().Be(6);
        result.Seats.Should().OnlyContain(s => s.Status == SeatStatus.Available);
    }

    [Fact]
    public async Task GetSeats_NonExistentTable_ReturnsNotFound()
    {
        // Arrange
        var nonExistentTableId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/tables/{nonExistentTableId}/seats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<GetSeatsResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    #endregion

    #region SelectSeat Tests

    [Fact]
    public async Task SelectSeat_ValidRequest_ReservesSpecificSeat()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Select Seat Test Table",
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
        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "TestPlayer");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SelectSeatResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.SeatNumber.Should().Be(3);
        result.ReservedUntil.Should().BeAfter(DateTime.UtcNow);
        result.Seat.Should().NotBeNull();
        result.Seat!.Status.Should().Be(SeatStatus.Reserved);
        result.Seat.PlayerName.Should().Be("TestPlayer");
    }

    [Fact]
    public async Task SelectSeat_OccupiedSeat_ReturnsBadRequest()
    {
        // Arrange - Create a table with seats
        var createRequest = new CreateTableRequest(
            Name: "Occupied Seat Test Table",
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

        // First player selects and buys in
        var selectRequest1 = new SelectSeatRequest(tableId, SeatNumber: 3, "Player1");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest1);
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "Player1", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Act - Second player tries to select the same seat
        var selectRequest2 = new SelectSeatRequest(tableId, SeatNumber: 3, "Player2");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<SelectSeatResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not available");
    }

    [Fact]
    public async Task SelectSeat_InvalidSeatNumber_ReturnsBadRequest()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Invalid Seat Number Test Table",
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

        // Act - Try to select seat 10 (invalid for 6-seat table)
        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 10, "TestPlayer");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<SelectSeatResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("between 1 and");
    }

    [Fact]
    public async Task SelectSeat_PlayerAlreadySeated_ReturnsBadRequest()
    {
        // Arrange - Create a table and seat a player
        var createRequest = new CreateTableRequest(
            Name: "Already Seated Test Table",
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

        // First seat selection
        var selectRequest1 = new SelectSeatRequest(tableId, SeatNumber: 1, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest1);
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 1, "TestPlayer", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Act - Same player tries to select another seat
        var selectRequest2 = new SelectSeatRequest(tableId, SeatNumber: 2, "TestPlayer");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<SelectSeatResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("already seated");
    }

    #endregion

    #region BuyIn Tests

    [Fact]
    public async Task BuyIn_ValidRequest_CompletesSuccessfully()
    {
        // Arrange - Create a table and select a seat
        var createRequest = new CreateTableRequest(
            Name: "Buy In Test Table",
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

        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);

        // Act
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "TestPlayer", BuyInAmount: 100);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<BuyInResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.SeatNumber.Should().Be(3);
        result.ChipStack.Should().Be(100);
        result.Seat.Should().NotBeNull();
        result.Seat!.Status.Should().Be(SeatStatus.Occupied);
        result.Seat.PlayerName.Should().Be("TestPlayer");
        result.Seat.ChipStack.Should().Be(100);
    }

    [Fact]
    public async Task BuyIn_BelowMinimum_ReturnsBadRequest()
    {
        // Arrange - Create a table and select a seat
        var createRequest = new CreateTableRequest(
            Name: "Below Minimum Buy In Test",
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

        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);

        // Act - Try to buy in below minimum
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "TestPlayer", BuyInAmount: 20);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<BuyInResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("at least");
    }

    [Fact]
    public async Task BuyIn_AboveMaximum_ReturnsBadRequest()
    {
        // Arrange - Create a table and select a seat
        var createRequest = new CreateTableRequest(
            Name: "Above Maximum Buy In Test",
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

        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);

        // Act - Try to buy in above maximum
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "TestPlayer", BuyInAmount: 500);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<BuyInResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("cannot exceed");
    }

    [Fact]
    public async Task BuyIn_WrongSeat_ReturnsBadRequest()
    {
        // Arrange - Create a table and select a seat
        var createRequest = new CreateTableRequest(
            Name: "Wrong Seat Buy In Test",
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

        // Player1 reserves seat 3
        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "Player1");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);

        // Act - Player2 tries to buy in at seat 3
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "Player2", BuyInAmount: 100);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<BuyInResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("reserved for another player");
    }

    #endregion

    #region SitOut Tests

    [Fact]
    public async Task SitOut_ValidRequest_MarksPlayerSittingOut()
    {
        // Arrange - Create a table, select seat, and buy in
        var createRequest = new CreateTableRequest(
            Name: "Sit Out Test Table",
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

        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "TestPlayer", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Act
        var sitOutRequest = new SitOutRequest(tableId, "TestPlayer", SitOut: true);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/sit-out", sitOutRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SitOutResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.IsSittingOut.Should().BeTrue();
    }

    [Fact]
    public async Task SitBackIn_ValidRequest_MarksPlayerActive()
    {
        // Arrange - Create a table, select seat, buy in, and sit out
        var createRequest = new CreateTableRequest(
            Name: "Sit Back In Test Table",
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

        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "TestPlayer", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);
        var sitOutRequest = new SitOutRequest(tableId, "TestPlayer", SitOut: true);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/sit-out", sitOutRequest);

        // Act - Sit back in
        var sitBackInRequest = new SitOutRequest(tableId, "TestPlayer", SitOut: false);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/sit-out", sitBackInRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SitOutResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.IsSittingOut.Should().BeFalse();
    }

    [Fact]
    public async Task SitOut_PlayerNotSeated_ReturnsBadRequest()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Sit Out Not Seated Test Table",
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

        // Act - Try to sit out without being seated
        var sitOutRequest = new SitOutRequest(tableId, "NonExistentPlayer", SitOut: true);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/sit-out", sitOutRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<SitOutResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not seated");
    }

    #endregion

    #region SeatChange Tests

    [Fact]
    public async Task SeatChange_ToAvailableSeat_MovesImmediately()
    {
        // Arrange - Create a table, select seat, and buy in
        var createRequest = new CreateTableRequest(
            Name: "Seat Change Test Table",
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

        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 1, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 1, "TestPlayer", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Act - Request seat change to seat 4
        var seatChangeRequest = new SeatChangeRequest(tableId, "TestPlayer", DesiredSeatNumber: 4);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/change", seatChangeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SeatChangeResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.OldSeatNumber.Should().Be(1);
        result.NewSeatNumber.Should().Be(4);
        result.IsPending.Should().BeFalse();
    }

    [Fact]
    public async Task SeatChange_ToOccupiedSeat_IsPending()
    {
        // Arrange - Create a table with two seated players
        var createRequest = new CreateTableRequest(
            Name: "Seat Change Pending Test Table",
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

        // Player1 at seat 1
        var selectRequest1 = new SelectSeatRequest(tableId, SeatNumber: 1, "Player1");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest1);
        var buyInRequest1 = new BuyInRequest(tableId, SeatNumber: 1, "Player1", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest1);

        // Player2 at seat 2
        var selectRequest2 = new SelectSeatRequest(tableId, SeatNumber: 2, "Player2");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest2);
        var buyInRequest2 = new BuyInRequest(tableId, SeatNumber: 2, "Player2", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest2);

        // Act - Player1 requests seat 2 (occupied by Player2)
        var seatChangeRequest = new SeatChangeRequest(tableId, "Player1", DesiredSeatNumber: 2);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/change", seatChangeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SeatChangeResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.IsPending.Should().BeTrue();
    }

    [Fact]
    public async Task SeatChange_ToSameSeat_ReturnsBadRequest()
    {
        // Arrange - Create a table and seat a player
        var createRequest = new CreateTableRequest(
            Name: "Seat Change Same Seat Test Table",
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

        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "TestPlayer", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Act - Try to change to same seat
        var seatChangeRequest = new SeatChangeRequest(tableId, "TestPlayer", DesiredSeatNumber: 3);
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/change", seatChangeRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<SeatChangeResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("already in the desired seat");
    }

    #endregion

    #region StandUp Tests

    [Fact]
    public async Task StandUp_ValidRequest_FreesSeaAndReturnsChips()
    {
        // Arrange - Create a table, select seat, and buy in
        var createRequest = new CreateTableRequest(
            Name: "Stand Up Test Table",
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

        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 3, "TestPlayer");
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 3, "TestPlayer", BuyInAmount: 100);
        await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);

        // Act
        var standUpRequest = new LeaveTableRequest(tableId, "TestPlayer");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/stand-up", standUpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LeaveTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();

        // Verify seat is now available
        var seatsResponse = await _client.GetAsync($"/api/tables/{tableId}/seats");
        var seatsResult = await seatsResponse.Content.ReadFromJsonAsync<GetSeatsResponse>();
        var seat3 = seatsResult!.Seats!.First(s => s.SeatNumber == 3);
        seat3.Status.Should().Be(SeatStatus.Available);
        seat3.PlayerName.Should().BeNull();
    }

    [Fact]
    public async Task StandUp_PlayerNotSeated_ReturnsBadRequest()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Stand Up Not Seated Test Table",
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

        // Act - Try to stand up without being seated
        var standUpRequest = new LeaveTableRequest(tableId, "NonExistentPlayer");
        var response = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/stand-up", standUpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<LeaveTableResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not seated");
    }

    #endregion

    #region Full Flow Tests

    [Fact]
    public async Task FullSeatingFlow_SelectSeatBuyInPlayStandUp_WorksCorrectly()
    {
        // Arrange - Create a table
        var createRequest = new CreateTableRequest(
            Name: "Full Flow Test Table",
            Variant: PokerVariant.TexasHoldem,
            SmallBlind: 5,
            BigBlind: 10,
            MinBuyIn: 200,
            MaxBuyIn: 1000,
            MaxSeats: 9,
            Privacy: TablePrivacy.Public);

        var createResponse = await _client.PostAsJsonAsync("/api/tables", createRequest);
        var createResult = await createResponse.Content.ReadFromJsonAsync<CreateTableResponse>();
        var tableId = createResult!.TableId!.Value;

        // Step 1: Get available seats
        var seatsResponse1 = await _client.GetAsync($"/api/tables/{tableId}/seats");
        var seatsResult1 = await seatsResponse1.Content.ReadFromJsonAsync<GetSeatsResponse>();
        seatsResult1!.Seats!.Should().OnlyContain(s => s.Status == SeatStatus.Available);

        // Step 2: Select seat 5
        var selectRequest = new SelectSeatRequest(tableId, SeatNumber: 5, "FullFlowPlayer");
        var selectResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/select", selectRequest);
        var selectResult = await selectResponse.Content.ReadFromJsonAsync<SelectSeatResponse>();
        selectResult!.Success.Should().BeTrue();
        selectResult.Seat!.Status.Should().Be(SeatStatus.Reserved);

        // Step 3: Complete buy-in
        var buyInRequest = new BuyInRequest(tableId, SeatNumber: 5, "FullFlowPlayer", BuyInAmount: 500);
        var buyInResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/buy-in", buyInRequest);
        var buyInResult = await buyInResponse.Content.ReadFromJsonAsync<BuyInResponse>();
        buyInResult!.Success.Should().BeTrue();
        buyInResult.ChipStack.Should().Be(500);
        buyInResult.Seat!.Status.Should().Be(SeatStatus.Occupied);

        // Step 4: Sit out
        var sitOutRequest = new SitOutRequest(tableId, "FullFlowPlayer", SitOut: true);
        var sitOutResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/sit-out", sitOutRequest);
        var sitOutResult = await sitOutResponse.Content.ReadFromJsonAsync<SitOutResponse>();
        sitOutResult!.Success.Should().BeTrue();
        sitOutResult.IsSittingOut.Should().BeTrue();

        // Step 5: Sit back in
        var sitBackInRequest = new SitOutRequest(tableId, "FullFlowPlayer", SitOut: false);
        var sitBackInResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/sit-out", sitBackInRequest);
        var sitBackInResult = await sitBackInResponse.Content.ReadFromJsonAsync<SitOutResponse>();
        sitBackInResult!.Success.Should().BeTrue();
        sitBackInResult.IsSittingOut.Should().BeFalse();

        // Step 6: Change seat to 7
        var seatChangeRequest = new SeatChangeRequest(tableId, "FullFlowPlayer", DesiredSeatNumber: 7);
        var seatChangeResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/change", seatChangeRequest);
        var seatChangeResult = await seatChangeResponse.Content.ReadFromJsonAsync<SeatChangeResponse>();
        seatChangeResult!.Success.Should().BeTrue();
        seatChangeResult.OldSeatNumber.Should().Be(5);
        seatChangeResult.NewSeatNumber.Should().Be(7);

        // Step 7: Verify current state
        var seatsResponse2 = await _client.GetAsync($"/api/tables/{tableId}/seats");
        var seatsResult2 = await seatsResponse2.Content.ReadFromJsonAsync<GetSeatsResponse>();
        var seat5 = seatsResult2!.Seats!.First(s => s.SeatNumber == 5);
        var seat7 = seatsResult2!.Seats!.First(s => s.SeatNumber == 7);
        seat5.Status.Should().Be(SeatStatus.Available);
        seat7.Status.Should().Be(SeatStatus.Occupied);
        seat7.PlayerName.Should().Be("FullFlowPlayer");

        // Step 8: Stand up
        var standUpRequest = new LeaveTableRequest(tableId, "FullFlowPlayer");
        var standUpResponse = await _client.PostAsJsonAsync($"/api/tables/{tableId}/seats/stand-up", standUpRequest);
        var standUpResult = await standUpResponse.Content.ReadFromJsonAsync<LeaveTableResponse>();
        standUpResult!.Success.Should().BeTrue();

        // Step 9: Verify final state
        var seatsResponse3 = await _client.GetAsync($"/api/tables/{tableId}/seats");
        var seatsResult3 = await seatsResponse3.Content.ReadFromJsonAsync<GetSeatsResponse>();
        seatsResult3!.Seats!.Should().OnlyContain(s => s.Status == SeatStatus.Available);
    }

    #endregion
}
