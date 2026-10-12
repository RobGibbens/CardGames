using System.Collections.Concurrent;
using CardGames.Core.French.Cards;
using CardGames.Core.French.Cards.Extensions;
using CardGames.Poker.Api.Data.Entities;

namespace CardGames.Poker.Api.Features.Testing;

public interface IScriptedDeckStore
{
	void Set(Guid gameId, IReadOnlyList<(CardSuit Suit, CardSymbol Symbol)> cards);

	bool TryConsume(Guid gameId, out IReadOnlyList<(CardSuit Suit, CardSymbol Symbol)> cards);
}

/// <summary>
///     Holds one ordered deck per game. The next initial deal consumes it.
/// </summary>
public sealed class ScriptedDeckStore : IScriptedDeckStore
{
	public const int DeckSize = 52;

	public static ScriptedDeckStore Instance { get; } = new();

	private readonly ConcurrentDictionary<Guid, IReadOnlyList<(CardSuit Suit, CardSymbol Symbol)>> _decks = new();

	public void Set(Guid gameId, IReadOnlyList<(CardSuit Suit, CardSymbol Symbol)> cards)
	{
		_decks[gameId] = cards.ToArray();
	}

	public bool TryConsume(Guid gameId, out IReadOnlyList<(CardSuit Suit, CardSymbol Symbol)> cards)
	{
		if (_decks.TryRemove(gameId, out var stored))
		{
			cards = stored;
			return true;
		}

		cards = [];
		return false;
	}

	public static bool TryParse(
		IReadOnlyList<string>? cards,
		out IReadOnlyList<(CardSuit Suit, CardSymbol Symbol)> deck,
		out string message)
	{
		deck = [];
		if (cards is null || cards.Count != DeckSize)
		{
			message = $"A scripted deck must contain exactly {DeckSize} cards.";
			return false;
		}

		var parsed = new List<(CardSuit Suit, CardSymbol Symbol)>(DeckSize);
		var seen = new HashSet<(CardSuit Suit, CardSymbol Symbol)>();
		foreach (var raw in cards)
		{
			if (!TryParseCard(raw, out var card, out message))
				return false;

			if (!seen.Add(card))
			{
				message = $"Card '{raw}' is repeated.";
				return false;
			}

			parsed.Add(card);
		}

		deck = parsed;
		message = string.Empty;
		return true;
	}

	private static bool TryParseCard(
		string? raw,
		out (CardSuit Suit, CardSymbol Symbol) card,
		out string message)
	{
		card = default;
		if (string.IsNullOrWhiteSpace(raw))
		{
			message = "A card code is missing.";
			return false;
		}

		var text = raw.Trim();
		if (text.Length != 2)
		{
			message = $"'{raw}' is not a valid card. Use codes such as Ah or Td.";
			return false;
		}

		var normalized = $"{char.ToUpperInvariant(text[0])}{char.ToLowerInvariant(text[1])}";
		try
		{
			var french = normalized.ToCard();
			card = ((CardSuit)(int)french.Suit, (CardSymbol)(int)french.Symbol);
			message = string.Empty;
			return true;
		}
		catch (ArgumentException)
		{
			message = $"'{raw}' is not a valid card. Use codes such as Ah or Td.";
			return false;
		}
	}
}
