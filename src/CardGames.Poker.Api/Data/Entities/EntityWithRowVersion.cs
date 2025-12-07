using System.ComponentModel.DataAnnotations;

namespace CardGames.Poker.Api.Data.Entities;

public abstract class EntityWithRowVersion
{
	[ConcurrencyCheck]
	public uint RowVersion { get; set; }
}