using CardGames.Poker.Betting;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Betting;

public class PokerPlayerTests
{
    [Fact]
    public void Constructor_SetsInitialChips()
    {
        var player = new PokerPlayer("Alice", 1000);

        player.Name.Should().Be("Alice");
        player.ChipStack.Should().Be(1000);
        player.CurrentBet.Should().Be(0);
        player.HasFolded.Should().BeFalse();
        player.IsAllIn.Should().BeFalse();
        player.IsActive.Should().BeTrue();
    }

    [Fact]
    public void PlaceBet_ReducesChipStack()
    {
        var player = new PokerPlayer("Alice", 1000);

        var actual = player.PlaceBet(100);

        actual.Should().Be(100);
        player.ChipStack.Should().Be(900);
        player.CurrentBet.Should().Be(100);
    }

    [Fact]
    public void PlaceBet_WhenBetExceedsStack_BetsAllChips()
    {
        var player = new PokerPlayer("Alice", 100);

        var actual = player.PlaceBet(200);

        actual.Should().Be(100);
        player.ChipStack.Should().Be(0);
        player.CurrentBet.Should().Be(100);
        player.IsAllIn.Should().BeTrue();
    }

    [Fact]
    public void Fold_SetsHasFolded()
    {
        var player = new PokerPlayer("Alice", 1000);

        player.Fold();

        player.HasFolded.Should().BeTrue();
        player.IsActive.Should().BeFalse();
        player.CanAct.Should().BeFalse();
    }

    [Fact]
    public void AddChips_IncreasesStack()
    {
        var player = new PokerPlayer("Alice", 1000);

        player.AddChips(500);

        player.ChipStack.Should().Be(1500);
    }

    [Fact]
    public void ResetCurrentBet_ResetsBet()
    {
        var player = new PokerPlayer("Alice", 1000);
        player.PlaceBet(100);

        player.ResetCurrentBet();

        player.CurrentBet.Should().Be(0);
        player.ChipStack.Should().Be(900);
    }

    [Fact]
    public void ResetForNewHand_ResetsBetAndFoldStatus()
    {
        var player = new PokerPlayer("Alice", 1000);
        player.PlaceBet(100);
        player.Fold();

        player.ResetForNewHand();

        player.CurrentBet.Should().Be(0);
        player.HasFolded.Should().BeFalse();
    }

    [Fact]
    public void AmountToCall_ReturnsCorrectAmount()
    {
        var player = new PokerPlayer("Alice", 1000);
        player.PlaceBet(50);

        var amountToCall = player.AmountToCall(100);

        amountToCall.Should().Be(50);
    }

    [Fact]
    public void IsAllIn_WhenChipsAreZeroAndNotFolded()
    {
        var player = new PokerPlayer("Alice", 100);
        player.PlaceBet(100);

        player.IsAllIn.Should().BeTrue();
        player.IsActive.Should().BeFalse();
        player.CanAct.Should().BeFalse();
    }
}
