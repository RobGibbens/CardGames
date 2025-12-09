namespace CardGames.Poker.Api.Data.Entities;

public abstract class EntityWithRowVersion
{
	public byte[] RowVersion { get; set; } = [];
}