using CardGames.Core.French.Cards;

namespace CardGames.Poker.Games.Baseball;

/// <summary>
/// Represents the result of a buy-card action in Baseball.
/// </summary>
public class BuyCardResult
{
	public bool Success { get; init; }
	public string ErrorMessage { get; init; }
	public string PlayerName { get; init; }
	public bool Purchased { get; init; }
	public int AmountPaid { get; init; }
	public Card ExtraCard { get; init; }
}