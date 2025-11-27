using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Shared.Contracts.Requests;
using CardGames.Poker.Shared.Contracts.Responses;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using ApiMappingExtensions = CardGames.Poker.Api.Extensions.MappingExtensions;

namespace CardGames.Poker.Api.Features.Hands;

/// <summary>
/// Module for hand-related endpoints.
/// </summary>
public static class HandsModule
{
    public static IEndpointRouteBuilder MapHandsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hands")
            .WithTags("Hands")
            .WithOpenApi();

        group.MapPost("/deal", DealHand)
            .WithName("DealHand")
            .WithDescription("Deal a poker hand for the specified variant and number of players");

        group.MapPost("/evaluate", EvaluateHand)
            .WithName("EvaluateHand")
            .WithDescription("Evaluate a 5-card poker hand");

        return app;
    }

    private static IResult DealHand(DealHandRequest request)
    {
        if (request.NumberOfPlayers < 2 || request.NumberOfPlayers > 10)
        {
            return Results.BadRequest("Number of players must be between 2 and 10");
        }

        var playerNames = request.PlayerNames?.ToList()
            ?? Enumerable.Range(1, request.NumberOfPlayers).Select(i => $"Player {i}").ToList();

        if (playerNames.Count != request.NumberOfPlayers)
        {
            return Results.BadRequest("Number of player names must match number of players");
        }

        return request.Variant switch
        {
            PokerVariant.TexasHoldem => DealHoldemHand(playerNames),
            PokerVariant.Omaha => DealOmahaHand(playerNames),
            PokerVariant.SevenCardStud => DealStudHand(playerNames),
            PokerVariant.FiveCardDraw => DealDrawHand(playerNames),
            _ => Results.BadRequest($"Unsupported variant: {request.Variant}")
        };
    }

    private static IResult DealHoldemHand(List<string> playerNames)
    {
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        var playerHands = new Dictionary<string, IReadOnlyCollection<Card>>();
        foreach (var name in playerNames)
        {
            playerHands[name] = dealer.DealCards(2);
        }

        // Deal flop, turn, river
        dealer.DealCard(); // burn
        var flop = dealer.DealCards(3);
        dealer.DealCard(); // burn
        var turn = dealer.DealCard();
        dealer.DealCard(); // burn
        var river = dealer.DealCard();

        var communityCards = flop.Concat(new[] { turn, river }).ToList();

        // Evaluate hands
        var evaluatedHands = playerHands.ToDictionary(
            kvp => kvp.Key,
            kvp => new HoldemHand(kvp.Value, communityCards));

        var maxStrength = evaluatedHands.Max(kvp => kvp.Value.Strength);
        var winners = evaluatedHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key).ToList();
        var winningDescription = HandDescriptionFormatter.GetHandDescription(evaluatedHands[winners.First()]);

        var players = evaluatedHands.Select(kvp => new PlayerDto(
            Name: kvp.Key,
            HoleCards: playerHands[kvp.Key].ToDtos(),
            Hand: kvp.Value.ToDto(HandDescriptionFormatter.GetHandDescription(kvp.Value))
        )).ToList();

        return Results.Ok(new DealHandResponse(
            Variant: PokerVariant.TexasHoldem,
            Players: players,
            CommunityCards: communityCards.ToDtos(),
            Winners: winners,
            WinningHandDescription: winningDescription
        ));
    }

    private static IResult DealOmahaHand(List<string> playerNames)
    {
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        var playerHands = new Dictionary<string, IReadOnlyCollection<Card>>();
        foreach (var name in playerNames)
        {
            playerHands[name] = dealer.DealCards(4);
        }

        // Deal flop, turn, river
        dealer.DealCard(); // burn
        var flop = dealer.DealCards(3);
        dealer.DealCard(); // burn
        var turn = dealer.DealCard();
        dealer.DealCard(); // burn
        var river = dealer.DealCard();

        var communityCards = flop.Concat(new[] { turn, river }).ToList();

        // Evaluate hands
        var evaluatedHands = playerHands.ToDictionary(
            kvp => kvp.Key,
            kvp => new OmahaHand(kvp.Value, communityCards));

        var maxStrength = evaluatedHands.Max(kvp => kvp.Value.Strength);
        var winners = evaluatedHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key).ToList();
        var winningDescription = HandDescriptionFormatter.GetHandDescription(evaluatedHands[winners.First()]);

        var players = evaluatedHands.Select(kvp => new PlayerDto(
            Name: kvp.Key,
            HoleCards: playerHands[kvp.Key].ToDtos(),
            Hand: kvp.Value.ToDto(HandDescriptionFormatter.GetHandDescription(kvp.Value))
        )).ToList();

        return Results.Ok(new DealHandResponse(
            Variant: PokerVariant.Omaha,
            Players: players,
            CommunityCards: communityCards.ToDtos(),
            Winners: winners,
            WinningHandDescription: winningDescription
        ));
    }

    private static IResult DealStudHand(List<string> playerNames)
    {
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        var playerHoleCards = new Dictionary<string, List<Card>>();
        var playerBoardCards = new Dictionary<string, List<Card>>();

        foreach (var name in playerNames)
        {
            playerHoleCards[name] = [];
            playerBoardCards[name] = [];
        }

        // Third Street: 2 hole cards and 1 board card
        foreach (var name in playerNames)
        {
            playerHoleCards[name].AddRange(dealer.DealCards(2));
            playerBoardCards[name].Add(dealer.DealCard());
        }

        // Fourth through Sixth Street: 1 board card each
        for (int street = 4; street <= 6; street++)
        {
            foreach (var name in playerNames)
            {
                playerBoardCards[name].Add(dealer.DealCard());
            }
        }

        // Seventh Street: 1 hole card (face down)
        foreach (var name in playerNames)
        {
            playerHoleCards[name].Add(dealer.DealCard());
        }

        // Evaluate hands
        var evaluatedHands = playerNames.ToDictionary(
            name => name,
            name => new SevenCardStudHand(
                playerHoleCards[name].Take(2).ToList(),
                playerBoardCards[name],
                playerHoleCards[name].Last()));

        var maxStrength = evaluatedHands.Max(kvp => kvp.Value.Strength);
        var winners = evaluatedHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key).ToList();
        var winningDescription = HandDescriptionFormatter.GetHandDescription(evaluatedHands[winners.First()]);

        var players = evaluatedHands.Select(kvp => new PlayerDto(
            Name: kvp.Key,
            HoleCards: playerHoleCards[kvp.Key].Concat(playerBoardCards[kvp.Key]).ToDtos(),
            Hand: kvp.Value.ToDto(HandDescriptionFormatter.GetHandDescription(kvp.Value))
        )).ToList();

        return Results.Ok(new DealHandResponse(
            Variant: PokerVariant.SevenCardStud,
            Players: players,
            CommunityCards: null,
            Winners: winners,
            WinningHandDescription: winningDescription
        ));
    }

    private static IResult DealDrawHand(List<string> playerNames)
    {
        var dealer = FrenchDeckDealer.WithFullDeck();
        dealer.Shuffle();

        var playerHands = new Dictionary<string, IReadOnlyCollection<Card>>();
        foreach (var name in playerNames)
        {
            playerHands[name] = dealer.DealCards(5);
        }

        // Evaluate hands
        var evaluatedHands = playerHands.ToDictionary(
            kvp => kvp.Key,
            kvp => new DrawHand(kvp.Value));

        var maxStrength = evaluatedHands.Max(kvp => kvp.Value.Strength);
        var winners = evaluatedHands.Where(kvp => kvp.Value.Strength == maxStrength).Select(kvp => kvp.Key).ToList();
        var winningDescription = HandDescriptionFormatter.GetHandDescription(evaluatedHands[winners.First()]);

        var players = evaluatedHands.Select(kvp => new PlayerDto(
            Name: kvp.Key,
            HoleCards: playerHands[kvp.Key].ToDtos(),
            Hand: kvp.Value.ToDto(HandDescriptionFormatter.GetHandDescription(kvp.Value))
        )).ToList();

        return Results.Ok(new DealHandResponse(
            Variant: PokerVariant.FiveCardDraw,
            Players: players,
            CommunityCards: null,
            Winners: winners,
            WinningHandDescription: winningDescription
        ));
    }

    private static IResult EvaluateHand(EvaluateHandRequest request)
    {
        if (request.Cards.Count != 5)
        {
            return Results.BadRequest("Hand must contain exactly 5 cards");
        }

        try
        {
            var cards = request.Cards.Select(c => c.ToCard()).ToList();
            var hand = new DrawHand(cards);
            var description = HandDescriptionFormatter.GetHandDescription(hand);

            return Results.Ok(new EvaluateHandResponse(
                Cards: cards.ToDtos(),
                HandType: ApiMappingExtensions.MapHandType(hand.Type),
                Description: description,
                Strength: hand.Strength
            ));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest($"Invalid card format: {ex.Message}");
        }
    }
}
