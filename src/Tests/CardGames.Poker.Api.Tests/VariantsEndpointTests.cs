using System.Net;
using System.Net.Http.Json;
using CardGames.Poker.Api.Features.Variants;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CardGames.Poker.Api.Tests;

public class VariantsEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public VariantsEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetVariants_ReturnsRegisteredVariants()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variants.Should().NotBeNull();
        result.Variants!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetVariants_IncludesTexasHoldem()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Variants.Should().Contain(v => v.Id == "texas-holdem");
        
        var holdem = result.Variants!.First(v => v.Id == "texas-holdem");
        holdem.Name.Should().Be("Texas Hold'em");
        holdem.Description.Should().NotBeNullOrEmpty();
        holdem.MinPlayers.Should().Be(2);
        holdem.MaxPlayers.Should().Be(10);
    }

    [Fact]
    public async Task GetVariants_IncludesOmaha()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Variants.Should().Contain(v => v.Id == "omaha");
        
        var omaha = result.Variants!.First(v => v.Id == "omaha");
        omaha.Name.Should().Be("Omaha");
        omaha.Description.Should().NotBeNullOrEmpty();
        omaha.MinPlayers.Should().Be(2);
        omaha.MaxPlayers.Should().Be(10);
    }

    [Fact]
    public async Task GetVariantById_ExistingVariant_ReturnsVariant()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/texas-holdem");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be("texas-holdem");
        result.Variant.Name.Should().Be("Texas Hold'em");
    }

    [Fact]
    public async Task GetVariantById_CaseInsensitive_ReturnsVariant()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/TEXAS-HOLDEM");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be("texas-holdem");
    }

    [Fact]
    public async Task GetVariantById_NonExistentVariant_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/non-existent-variant");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task GetVariantById_OmahaVariant_ReturnsVariant()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/omaha");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be("omaha");
        result.Variant.Name.Should().Be("Omaha");
    }

    [Fact]
    public async Task GetVariants_IncludesSevenCardStud()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Variants.Should().Contain(v => v.Id == "seven-card-stud");
        
        var sevenCardStud = result.Variants!.First(v => v.Id == "seven-card-stud");
        sevenCardStud.Name.Should().Be("Seven Card Stud");
        sevenCardStud.Description.Should().NotBeNullOrEmpty();
        sevenCardStud.MinPlayers.Should().Be(2);
        sevenCardStud.MaxPlayers.Should().Be(7);
    }

    [Fact]
    public async Task GetVariantById_SevenCardStudVariant_ReturnsVariant()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/seven-card-stud");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be("seven-card-stud");
        result.Variant.Name.Should().Be("Seven Card Stud");
    }

    [Fact]
    public async Task GetVariants_IncludesFiveCardDraw()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Variants.Should().Contain(v => v.Id == "five-card-draw");
        
        var fiveCardDraw = result.Variants!.First(v => v.Id == "five-card-draw");
        fiveCardDraw.Name.Should().Be("Five Card Draw");
        fiveCardDraw.Description.Should().NotBeNullOrEmpty();
        fiveCardDraw.MinPlayers.Should().Be(2);
        fiveCardDraw.MaxPlayers.Should().Be(6);
    }

    [Fact]
    public async Task GetVariantById_FiveCardDrawVariant_ReturnsVariant()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/five-card-draw");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be("five-card-draw");
        result.Variant.Name.Should().Be("Five Card Draw");
    }

    [Fact]
    public async Task GetVariants_IncludesFollowTheQueen()
    {
        // Act
        var response = await _client.GetAsync("/api/variants");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantsListResponse>();
        result.Should().NotBeNull();
        result!.Variants.Should().Contain(v => v.Id == "follow-the-queen");
        
        var followTheQueen = result.Variants!.First(v => v.Id == "follow-the-queen");
        followTheQueen.Name.Should().Be("Follow the Queen");
        followTheQueen.Description.Should().NotBeNullOrEmpty();
        followTheQueen.MinPlayers.Should().Be(2);
        followTheQueen.MaxPlayers.Should().Be(7);
    }

    [Fact]
    public async Task GetVariantById_FollowTheQueenVariant_ReturnsVariant()
    {
        // Act
        var response = await _client.GetAsync("/api/variants/follow-the-queen");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<VariantResponse>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Variant.Should().NotBeNull();
        result.Variant!.Id.Should().Be("follow-the-queen");
        result.Variant.Name.Should().Be("Follow the Queen");
    }
}
