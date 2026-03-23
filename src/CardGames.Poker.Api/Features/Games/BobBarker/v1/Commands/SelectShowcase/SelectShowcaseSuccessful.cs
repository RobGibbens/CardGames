using CardGames.Poker.Api.Infrastructure;

namespace CardGames.Poker.Api.Features.Games.BobBarker.v1.Commands.SelectShowcase;

public record SelectShowcaseSuccessful : IPlayerActionResult
{
    public Guid GameId { get; init; }

    public required string PlayerName { get; init; }

    public int PlayerSeatIndex { get; init; }

    public int ShowcaseCardIndex { get; init; }

    public bool SelectionPhaseComplete { get; init; }

    public required string CurrentPhase { get; init; }

    public int NextPlayerSeatIndex { get; init; }

    public string? NextPlayerName { get; init; }

    string? IPlayerActionResult.PlayerName => PlayerName;

    string IPlayerActionResult.ActionDescription => "Selected showcase";
}