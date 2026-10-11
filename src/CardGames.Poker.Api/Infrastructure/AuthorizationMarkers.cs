namespace CardGames.Poker.Api.Infrastructure;

/// <summary>
///     HTTP callers must be authenticated. In-process MediatR calls with no HTTP context
///     (timers, continuous play) are not subject to this check.
/// </summary>
public interface IRequiresAuthentication;

/// <summary>
///     Caller must resolve to a seated player who has not left the table.
/// </summary>
public interface IRequiresSeatedPlayer : IRequiresAuthentication;

/// <summary>
///     Caller must be the current betting or draw actor.
/// </summary>
public interface IRequiresCurrentActor : IRequiresSeatedPlayer;

/// <summary>
///     The seat this command may act for. Null means no seat was approved.
/// </summary>
public interface IBoundToActorSeat
{
	int? BoundSeat { get; set; }
}

/// <summary>
///     Caller must be seated or the table host/creator (start hand, deal, antes).
/// </summary>
public interface IRequiresHostOrSeatedPlayer : IRequiresAuthentication;
