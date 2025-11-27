using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Requests;
using CardGames.Poker.Shared.Contracts.Responses;
using CardGames.Poker.Shared.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class HandsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HandsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DealHand_WithHoldem_ReturnsValidResponse()
    {
        // Arrange
        var request = new DealHandRequest(
            Variant: PokerVariant.TexasHoldem,
            NumberOfPlayers: 3,
            PlayerNames: ["Alice", "Bob", "Charlie"]
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/hands/deal", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DealHandResponse>();
        result.Should().NotBeNull();
        result!.Variant.Should().Be(PokerVariant.TexasHoldem);
        result.Players.Should().HaveCount(3);
        result.CommunityCards.Should().HaveCount(5);
        result.Winners.Should().NotBeEmpty();
        result.WinningHandDescription.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task DealHand_WithFiveCardDraw_ReturnsValidResponse()
    {
        // Arrange
        var request = new DealHandRequest(
            Variant: PokerVariant.FiveCardDraw,
            NumberOfPlayers: 2
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/hands/deal", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<DealHandResponse>();
        result.Should().NotBeNull();
        result!.Variant.Should().Be(PokerVariant.FiveCardDraw);
        result.Players.Should().HaveCount(2);
        result.CommunityCards.Should().BeNull();
    }

    [Fact]
    public async Task DealHand_WithInvalidPlayerCount_ReturnsBadRequest()
    {
        // Arrange
        var request = new DealHandRequest(
            Variant: PokerVariant.TexasHoldem,
            NumberOfPlayers: 15
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/hands/deal", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task EvaluateHand_WithRoyalFlush_ReturnsCorrectHandType()
    {
        // Arrange
        var request = new EvaluateHandRequest(["As", "Ks", "Qs", "Js", "Ts"]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/hands/evaluate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EvaluateHandResponse>();
        result.Should().NotBeNull();
        result!.HandType.Should().Be("Straight Flush");
        result.Description.Should().Contain("Royal Flush");
    }

    [Fact]
    public async Task EvaluateHand_WithPair_ReturnsCorrectHandType()
    {
        // Arrange
        var request = new EvaluateHandRequest(["As", "Ah", "Kd", "Qc", "Js"]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/hands/evaluate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<EvaluateHandResponse>();
        result.Should().NotBeNull();
        result!.HandType.Should().Be("One Pair");
        result.Description.Should().Contain("Aces");
    }

    [Fact]
    public async Task EvaluateHand_WithInvalidCardCount_ReturnsBadRequest()
    {
        // Arrange
        var request = new EvaluateHandRequest(["As", "Ks", "Qs"]);

        // Act
        var response = await _client.PostAsJsonAsync("/api/hands/evaluate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
