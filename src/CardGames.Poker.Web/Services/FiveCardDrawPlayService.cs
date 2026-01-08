using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;
using Refit;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Gameplay service for Five Card Draw poker.
/// </summary>
public class FiveCardDrawPlayService : IGamePlayService
{
	private readonly IFiveCardDrawApi _api;

	public FiveCardDrawPlayService(IFiveCardDrawApi api)
	{
		_api = api ?? throw new ArgumentNullException(nameof(api));
	}

	/// <inheritdoc />
	public bool SupportsDraw => true;

	/// <inheritdoc />
	public string GameTypeCode => "FIVECARDDRAW";

	/// <inheritdoc />
	public async Task<ProcessBettingActionSuccessful> ProcessBettingActionAsync(
		Guid gameId,
		ProcessBettingActionRequest request,
		CancellationToken cancellationToken = default)
	{
		var response = await _api.FiveCardDrawProcessBettingActionAsync(gameId, request, cancellationToken);
		return response.Content ?? throw new InvalidOperationException("API returned null content");
	}

	/// <inheritdoc />
	public async Task<ProcessDrawSuccessful> ProcessDrawAsync(
		Guid gameId,
		ProcessDrawRequest request,
		CancellationToken cancellationToken = default)
	{
		var response = await _api.FiveCardDrawProcessDrawAsync(gameId, request, cancellationToken);
		return response.Content ?? throw new InvalidOperationException("API returned null content");
	}
}
