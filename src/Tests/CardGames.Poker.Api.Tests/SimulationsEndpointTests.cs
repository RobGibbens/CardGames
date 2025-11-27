using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Shared.Contracts.Requests;
using CardGames.Poker.Shared.Contracts.Responses;
using CardGames.Poker.Shared.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class SimulationsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SimulationsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RunSimulation_WithHoldem_ReturnsValidResponse()
    {
        // Arrange
        var request = new RunSimulationRequest(
            Variant: PokerVariant.TexasHoldem,
            NumberOfHands: 100,
            Players: [
                new SimulationPlayerRequest("Player 1", ["As", "Ah"]),
                new SimulationPlayerRequest("Player 2", ["Ks", "Kh"])
            ]
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/simulations/run", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SimulationResultResponse>();
        result.Should().NotBeNull();
        result!.Variant.Should().Be(PokerVariant.TexasHoldem);
        result.TotalHands.Should().Be(100);
        result.PlayerResults.Should().HaveCount(2);
        result.HandDistributions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RunSimulation_WithFlop_ReturnsValidResponse()
    {
        // Arrange
        var request = new RunSimulationRequest(
            Variant: PokerVariant.TexasHoldem,
            NumberOfHands: 50,
            Players: [
                new SimulationPlayerRequest("Player 1", ["As", "Ah"]),
                new SimulationPlayerRequest("Player 2", ["Ks", "Kh"])
            ],
            FlopCards: ["Ac", "7d", "2c"]
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/simulations/run", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SimulationResultResponse>();
        result.Should().NotBeNull();
        result!.TotalHands.Should().Be(50);
        // Player 1 should have a significant advantage with trip Aces
        result.PlayerResults.First(p => p.Name == "Player 1").WinPercentage.Should().BeGreaterThan(50);
    }

    [Fact]
    public async Task RunSimulation_WithTooFewPlayers_ReturnsBadRequest()
    {
        // Arrange
        var request = new RunSimulationRequest(
            Variant: PokerVariant.TexasHoldem,
            NumberOfHands: 100,
            Players: [
                new SimulationPlayerRequest("Player 1", ["As", "Ah"])
            ]
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/simulations/run", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RunSimulation_WithTooManyHands_ReturnsBadRequest()
    {
        // Arrange
        var request = new RunSimulationRequest(
            Variant: PokerVariant.TexasHoldem,
            NumberOfHands: 200000,
            Players: [
                new SimulationPlayerRequest("Player 1", ["As", "Ah"]),
                new SimulationPlayerRequest("Player 2", ["Ks", "Kh"])
            ]
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/simulations/run", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
