using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Contracts;

namespace CardGames.Poker.Web.Services;

/// <summary>
/// Gameplay service for Seven Card Stud poker.
/// </summary>
public class SevenCardStudPlayService : IGamePlayService
{
	private readonly ISevenCardStudApi _api;

	public SevenCardStudPlayService(ISevenCardStudApi api)
	{
		_api = api ?? throw new ArgumentNullException(nameof(api));
	}

	/// <inheritdoc />
	public bool SupportsDraw => false;

	/// <inheritdoc />
	public string GameTypeCode => "SEVENCARDSTUD";

	/// <inheritdoc />
	public async Task<ProcessBettingActionSuccessful> ProcessBettingActionAsync(
		Guid gameId,
		ProcessBettingActionRequest request,
		CancellationToken cancellationToken = default)
	{
		var response = await _api.SevenCardStudProcessBettingActionAsync(gameId, request, cancellationToken);
		return response.Content ?? throw new InvalidOperationException("API returned null content");
	}

	/// <inheritdoc />
	public Task<ProcessDrawSuccessful> ProcessDrawAsync(
		Guid gameId,
		ProcessDrawRequest request,
		CancellationToken cancellationToken = default)
	{
		throw new NotSupportedException("Seven Card Stud does not support draw actions");
	}
}
