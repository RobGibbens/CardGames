namespace CardGames.Poker.Api.Features.Games.Generic.v1.Queries.GetGeneratedTableName;

/// <summary>
/// Response for a generated table name suggestion.
/// </summary>
/// <param name="Name">The generated display name for the table.</param>
public sealed record GetGeneratedTableNameResponse(string Name);