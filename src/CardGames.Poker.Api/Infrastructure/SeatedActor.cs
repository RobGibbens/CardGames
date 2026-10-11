namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
///     Maps a resolved seat to HTTP 403 when an endpoint must ignore a client-supplied player id.
/// </summary>
public static class SeatedActor
{
	public static IResult NotSeated() =>
		Results.Json(new { message = "Not seated at this table." }, statusCode: StatusCodes.Status403Forbidden);
}
