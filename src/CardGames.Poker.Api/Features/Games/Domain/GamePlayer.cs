namespace CardGames.Poker.Api.Features.Games.Domain;

/// <summary>
/// Represents a player in the game aggregate.
/// </summary>
public class GamePlayer
{
	public Guid PlayerId { get; init; }
	public string Name { get; init; }
	public int ChipStack { get; set; }
	public int Position { get; init; }

	// NEW: Hand state
	public int CurrentBet { get; set; }
	public bool HasFolded { get; set; }
	public bool IsAllIn { get; set; }
	public List<string> Cards { get; set; } = [];

	public GamePlayer(Guid playerId, string name, int chipStack, int position)
	{
		PlayerId = playerId;
		Name = name;
		ChipStack = chipStack;
		Position = position;
	}

	public void DeductChips(int amount)
	{
		ChipStack -= Math.Min(amount, ChipStack);
	}

	public void AddChips(int amount)
	{
		ChipStack += amount;
	}

	public void ResetForNewHand()
	{
		CurrentBet = 0;
		HasFolded = false;
		IsAllIn = false;
		Cards.Clear();
	}
}