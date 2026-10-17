using System.Globalization;
using System.Text.RegularExpressions;

namespace CardGames.Poker.Web.Utilities;

/// <summary>
///     Formats integer chip amounts. Currency tables display one chip as one cent.
/// </summary>
public static partial class ChipAmountFormat
{
	private static readonly CultureInfo CurrencyCulture = CultureInfo.GetCultureInfo("en-US");

	public static string Format(int chips, bool asCurrency)
	{
		if (!asCurrency)
			return chips.ToString("N0", CurrencyCulture);

		return ToDollars(chips).ToString("C", CurrencyCulture);
	}

	public static string FormatSigned(int chips, bool asCurrency)
	{
		if (!asCurrency)
		{
			return chips switch
			{
				> 0 => $"+{chips.ToString("N0", CurrencyCulture)}",
				< 0 => chips.ToString("N0", CurrencyCulture),
				_ => "0"
			};
		}

		var formatted = ToDollars(Math.Abs(chips)).ToString("C", CurrencyCulture);
		return chips switch
		{
			> 0 => $"+{formatted}",
			< 0 => $"-{formatted}",
			_ => 0m.ToString("C", CurrencyCulture)
		};
	}

	public static string FormatActionDescription(string? description, bool asCurrency)
	{
		if (!asCurrency || string.IsNullOrWhiteSpace(description))
			return description ?? string.Empty;

		var match = ActionAmountPattern().Match(description);
		if (!match.Success || !int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var chips))
			return description;

		return $"{match.Groups[1].Value} {Format(chips, true)}";
	}

	public static decimal ToDollars(int chips) => chips / 100m;

	public static int FromDollars(decimal dollars) =>
		(int)Math.Round(dollars * 100m, MidpointRounding.AwayFromZero);

	public static string InputValue(int chips, bool asCurrency) =>
		asCurrency
			? ToDollars(chips).ToString("0.00", CultureInfo.InvariantCulture)
			: chips.ToString(CultureInfo.InvariantCulture);

	public static bool TryParseInput(string? text, bool asCurrency, out int chips)
	{
		chips = 0;
		if (string.IsNullOrWhiteSpace(text))
			return false;

		if (!asCurrency)
		{
			return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out chips)
			       || int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out chips);
		}

		if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var dollars)
		    && !decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out dollars))
			return false;

		chips = FromDollars(dollars);
		return true;
	}

	[GeneratedRegex(@"^(Called|Bet|Raised to) (\d+)$", RegexOptions.CultureInvariant)]
	private static partial Regex ActionAmountPattern();
}
