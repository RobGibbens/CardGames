using Refit;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Api.Clients;

/// <summary>
/// Manual extensions to the auto-generated IFiveCardDrawApi interface.
/// Add new API methods here until the Refitter output is regenerated.
/// </summary>
public partial interface IFiveCardDrawApi
{
    
}

/// <summary>
/// Request model for joining a game.
/// </summary>
/// <param name="SeatIndex">The zero-based seat index to occupy.</param>
/// <param name="StartingChips">The initial chip stack for the player. Defaults to 1000.</param>
public record JoinGameRequest(int SeatIndex, int StartingChips = 1000);

/// <summary>
/// Response for a successful join game operation.
/// </summary>
/// <param name="GameId">The unique identifier of the game.</param>
/// <param name="SeatIndex">The seat index the player was assigned to.</param>
/// <param name="PlayerName">The name of the player who joined.</param>
/// <param name="CanPlayCurrentHand">Whether the player can participate in the current hand.</param>
public record JoinGameSuccessful(
    Guid GameId,
    int SeatIndex,
    string PlayerName,
    string? PlayerAvatarUrl,
    string? PlayerFirstName,
	bool CanPlayCurrentHand);
