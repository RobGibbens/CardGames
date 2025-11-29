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
}
