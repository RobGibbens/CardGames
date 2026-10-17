using System.Text.Json.Serialization;
using CardGames.Poker.Api.Infrastructure;
using MediatR;
using OneOf;

namespace CardGames.Poker.Api.Features.Games.Common.v1.Commands.CreateGame;

public record CreateGameCommand(
	Guid GameId,
	string GameCode,
	string? GameName,
	int Ante,
	int MinBet,
	IReadOnlyList<PlayerInfo> Players,
	bool IsDealersChoice = false,
	int? SmallBlind = null,
	int? BigBlind = null,
	int? MaxBuyIn = null,
	int? TournamentBuyIn = null,
	bool RequiresJoinApproval = false,
	bool AreOddsVisibleToAllPlayers = true,
	bool DisplayChipsAsCurrency = false) : IRequest<OneOf<CreateGameSuccessful, CreateGameConflict>>,
	IGameStateChangingCommand, ILobbyStateChangingCommand
{
	[JsonPropertyName("allowedDealerChoiceGameCodes")]
	public IReadOnlyList<string>? AllowedDealerChoiceGameCodes { get; init; }

	[JsonPropertyName("defaultSmallBlind")]
	public int? DefaultSmallBlind { get; init; }

	[JsonPropertyName("defaultBigBlind")]
	public int? DefaultBigBlind { get; init; }

	[JsonPropertyName("defaultAnte")]
	public int? DefaultAnte { get; init; }
}