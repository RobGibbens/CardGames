using CardGames.Core.French.Cards;
using CardGames.Core.French.Dealers;
using CardGames.Poker.Api.Common.Mapping;
using CardGames.Poker.Api.Extensions;
using CardGames.Poker.Hands.CommunityCardHands;
using CardGames.Poker.Hands.DrawHands;
using CardGames.Poker.Hands.StudHands;
using CardGames.Poker.Shared.Contracts.Requests;
using CardGames.Poker.Shared.Contracts.Responses;
using CardGames.Poker.Shared.DTOs;
using CardGames.Poker.Shared.Enums;
using FluentValidation;
using MediatR;

namespace CardGames.Poker.Api.Features.Hands;

/// <summary>
/// Command to deal a poker hand.
/// </summary>
public record DealHandCommand(
    PokerVariant Variant,
    int NumberOfPlayers,
    IReadOnlyList<string>? PlayerNames = null) : IRequest<IResult>;

/// <summary>
/// Validator for DealHandCommand.
/// </summary>
public sealed class DealHandCommandValidator : AbstractValidator<DealHandCommand>
{
    public DealHandCommandValidator()
    {
        RuleFor(x => x.NumberOfPlayers)
            .InclusiveBetween(2, 10)
            .WithMessage("Number of players must be between 2 and 10");

        RuleFor(x => x.PlayerNames)
            .Must((cmd, names) => names == null || names.Count == cmd.NumberOfPlayers)
            .When(x => x.PlayerNames != null)
            .WithMessage("Number of player names must match number of players");
    }
}

/// <summary>
/// Handler for DealHandCommand.
/// </summary>
public sealed class DealHandHandler : IRequestHandler<DealHandCommand, IResult>
{
    public Task<IResult> Handle(DealHandCommand request, CancellationToken cancellationToken)
    {
        var playerNames = request.PlayerNames?.ToList()
            ?? Enumerable.Range(1, request.NumberOfPlayers).Select(i => $"Player {i}").ToList();

        var result = request.Variant switch
        {
            PokerVariant.TexasHoldem => DealHoldemHand(playerNames),
            PokerVariant.Omaha => DealOmahaHand(playerNames),
            PokerVariant.SevenCardStud => DealStudHand(playerNames),
            PokerVariant.FiveCardDraw => DealDrawHand(playerNames),
            _ => Results.BadRequest($"Unsupported variant: {request.Variant}")
        };

        return Task.FromResult(result);
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
            HoleCards: ApiMapper.ToCardDtos(playerHands[kvp.Key]),
            Hand: ApiMapper.ToHandDto(kvp.Value, HandDescriptionFormatter.GetHandDescription(kvp.Value))
        )).ToList();

        return Results.Ok(new DealHandResponse(
            Variant: PokerVariant.TexasHoldem,
            Players: players,
            CommunityCards: ApiMapper.ToCardDtos(communityCards),
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
            HoleCards: ApiMapper.ToCardDtos(playerHands[kvp.Key]),
            Hand: ApiMapper.ToHandDto(kvp.Value, HandDescriptionFormatter.GetHandDescription(kvp.Value))
        )).ToList();

        return Results.Ok(new DealHandResponse(
            Variant: PokerVariant.Omaha,
            Players: players,
            CommunityCards: ApiMapper.ToCardDtos(communityCards),
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
            HoleCards: ApiMapper.ToCardDtos(playerHoleCards[kvp.Key].Concat(playerBoardCards[kvp.Key])),
            Hand: ApiMapper.ToHandDto(kvp.Value, HandDescriptionFormatter.GetHandDescription(kvp.Value))
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
            HoleCards: ApiMapper.ToCardDtos(playerHands[kvp.Key]),
            Hand: ApiMapper.ToHandDto(kvp.Value, HandDescriptionFormatter.GetHandDescription(kvp.Value))
        )).ToList();

        return Results.Ok(new DealHandResponse(
            Variant: PokerVariant.FiveCardDraw,
            Players: players,
            CommunityCards: null,
            Winners: winners,
            WinningHandDescription: winningDescription
        ));
    }
}

/// <summary>
/// Endpoint for dealing hands.
/// </summary>
public static class DealHandEndpoint
{
    public static IEndpointRouteBuilder MapDealHandEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/hands/deal", async (
            DealHandRequest request,
            IMediator mediator,
            CancellationToken cancellationToken) =>
        {
            var command = new DealHandCommand(
                request.Variant,
                request.NumberOfPlayers,
                request.PlayerNames);

            return await mediator.Send(command, cancellationToken);
        })
        .WithName("DealHand")
        .WithDescription("Deal a poker hand for the specified variant and number of players")
        .WithTags("Hands");

        return app;
    }
}
