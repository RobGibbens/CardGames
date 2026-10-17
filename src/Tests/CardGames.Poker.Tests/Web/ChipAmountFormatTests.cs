using CardGames.Poker.Web.Utilities;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class ChipAmountFormatTests
{
	[Theory]
	[InlineData(100_000, "$1,000.00")]
	[InlineData(976, "$9.76")]
	[InlineData(26, "$0.26")]
	[InlineData(1, "$0.01")]
	[InlineData(0, "$0.00")]
	public void Format_Currency_ConvertsOneChipToOneCent(int chips, string expected)
	{
		ChipAmountFormat.Format(chips, asCurrency: true).Should().Be(expected);
	}

	[Fact]
	public void Format_Chips_UsesThousandsSeparators()
	{
		ChipAmountFormat.Format(100_000, asCurrency: false).Should().Be("100,000");
	}

	[Fact]
	public void FormatSigned_Currency_KeepsTheSign()
	{
		ChipAmountFormat.FormatSigned(976, asCurrency: true).Should().Be("+$9.76");
		ChipAmountFormat.FormatSigned(-50, asCurrency: true).Should().Be("-$0.50");
		ChipAmountFormat.FormatSigned(0, asCurrency: true).Should().Be("$0.00");
	}

	[Fact]
	public void FormatSigned_Chips_KeepsTheExistingSignStyle()
	{
		ChipAmountFormat.FormatSigned(976, asCurrency: false).Should().Be("+976");
		ChipAmountFormat.FormatSigned(-50, asCurrency: false).Should().Be("-50");
		ChipAmountFormat.FormatSigned(0, asCurrency: false).Should().Be("0");
	}

	[Theory]
	[InlineData("Called 26", "Called $0.26")]
	[InlineData("Bet 500", "Bet $5.00")]
	[InlineData("Raised to 100000", "Raised to $1,000.00")]
	[InlineData("Discarded 2", "Discarded 2")]
	[InlineData("Checked", "Checked")]
	public void FormatActionDescription_RewritesOnlyBettingAmounts(string description, string expected)
	{
		ChipAmountFormat.FormatActionDescription(description, asCurrency: true).Should().Be(expected);
	}

	[Fact]
	public void DollarConversion_RoundTripsWholeCents()
	{
		ChipAmountFormat.FromDollars(0.26m).Should().Be(26);
		ChipAmountFormat.FromDollars(1000m).Should().Be(100_000);
		ChipAmountFormat.ToDollars(26).Should().Be(0.26m);
	}

	[Fact]
	public void TryParseInput_Currency_AcceptsDollars()
	{
		ChipAmountFormat.TryParseInput("0.26", asCurrency: true, out var chips).Should().BeTrue();
		chips.Should().Be(26);
	}

	[Fact]
	public void TryParseInput_Chips_AcceptsIntegers()
	{
		ChipAmountFormat.TryParseInput("26", asCurrency: false, out var chips).Should().BeTrue();
		chips.Should().Be(26);
	}
}
