using System.Linq;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using CardGames.Poker.Games.GameFlow;
using CardGames.Poker.Games.Tollbooth;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Games;

public class TollboothRulesTests
{
	[Fact]
	public void CreateGameRules_ShouldReturnValidRules()
	{
		var rules = TollboothRules.CreateGameRules();

		rules.Should().NotBeNull();
		rules.GameTypeCode.Should().Be("TOLLBOOTH");
		rules.GameTypeName.Should().Be("Tollbooth");
	}

	[Fact]
	public void CreateGameRules_ShouldHaveCorrectPlayerLimits()
	{
		var rules = TollboothRules.CreateGameRules();

		rules.MinPlayers.Should().Be(2);
		rules.MaxPlayers.Should().Be(7);
	}

	[Fact]
	public void CreateGameRules_ShouldHaveCorrectPhaseCount()
	{
		// WaitingToStart, CollectingAntes, ThirdStreet, TollboothOffer,
		// FourthStreet, FifthStreet, SixthStreet, SeventhStreet, Showdown, Complete = 10
		var rules = TollboothRules.CreateGameRules();

		rules.Phases.Should().HaveCount(10);
	}

	[Fact]
	public void CreateGameRules_ShouldHaveCorrectPhaseOrder()
	{
		var rules = TollboothRules.CreateGameRules();

		rules.Phases[0].PhaseId.Should().Be("WaitingToStart");
		rules.Phases[1].PhaseId.Should().Be("CollectingAntes");
		rules.Phases[2].PhaseId.Should().Be("ThirdStreet");
		rules.Phases[3].PhaseId.Should().Be("TollboothOffer");
		rules.Phases[4].PhaseId.Should().Be("FourthStreet");
		rules.Phases[5].PhaseId.Should().Be("FifthStreet");
		rules.Phases[6].PhaseId.Should().Be("SixthStreet");
		rules.Phases[7].PhaseId.Should().Be("SeventhStreet");
		rules.Phases[8].PhaseId.Should().Be("Showdown");
		rules.Phases[9].PhaseId.Should().Be("Complete");
	}

	[Fact]
	public void CreateGameRules_TollboothOffer_ShouldBeDrawingCategory()
	{
		var rules = TollboothRules.CreateGameRules();

		var tollboothPhase = rules.Phases.Single(p => p.PhaseId == "TollboothOffer");
		tollboothPhase.Category.Should().Be("Drawing");
		tollboothPhase.RequiresPlayerAction.Should().BeTrue();
		tollboothPhase.AvailableActions.Should().Contain("ChooseFurthest");
		tollboothPhase.AvailableActions.Should().Contain("ChooseNearest");
		tollboothPhase.AvailableActions.Should().Contain("ChooseDeck");
	}

	[Fact]
	public void CreateGameRules_BettingPhases_ShouldRequirePlayerAction()
	{
		var rules = TollboothRules.CreateGameRules();

		rules.Phases.Single(p => p.PhaseId == "ThirdStreet").RequiresPlayerAction.Should().BeTrue();
		rules.Phases.Single(p => p.PhaseId == "FourthStreet").RequiresPlayerAction.Should().BeTrue();
		rules.Phases.Single(p => p.PhaseId == "FifthStreet").RequiresPlayerAction.Should().BeTrue();
		rules.Phases.Single(p => p.PhaseId == "SixthStreet").RequiresPlayerAction.Should().BeTrue();
		rules.Phases.Single(p => p.PhaseId == "SeventhStreet").RequiresPlayerAction.Should().BeTrue();
	}

	[Fact]
	public void CreateGameRules_BettingPhases_ShouldHaveCorrectCategory()
	{
		var rules = TollboothRules.CreateGameRules();

		rules.Phases.Single(p => p.PhaseId == "ThirdStreet").Category.Should().Be("Betting");
		rules.Phases.Single(p => p.PhaseId == "FourthStreet").Category.Should().Be("Betting");
		rules.Phases.Single(p => p.PhaseId == "FifthStreet").Category.Should().Be("Betting");
		rules.Phases.Single(p => p.PhaseId == "SixthStreet").Category.Should().Be("Betting");
		rules.Phases.Single(p => p.PhaseId == "SeventhStreet").Category.Should().Be("Betting");
	}

	[Fact]
	public void CreateGameRules_ShouldHaveCorrectBettingConfig()
	{
		var rules = TollboothRules.CreateGameRules();

		rules.Betting.HasAntes.Should().BeTrue();
		rules.Betting.HasBlinds.Should().BeFalse();
		rules.Betting.BettingRounds.Should().Be(5);
		rules.Betting.Structure.Should().Be("Fixed Limit");
	}

	[Fact]
	public void CreateGameRules_ShouldHaveCorrectCardDealingConfig()
	{
		var rules = TollboothRules.CreateGameRules();

		rules.CardDealing.InitialCards.Should().Be(3);
		rules.CardDealing.InitialVisibility.Should().Be(CardVisibility.Mixed);
		rules.CardDealing.HasCommunityCards.Should().BeFalse();
		rules.CardDealing.DealingRounds.Should().HaveCount(6);
	}

	[Fact]
	public void CreateGameRules_ShouldHaveTollboothSpecialRules()
	{
		var rules = TollboothRules.CreateGameRules();

		rules.SpecialRules.Should().ContainKey("TollboothOffer");
		rules.SpecialRules.Should().ContainKey("TollboothDisplayCards");
		rules.SpecialRules.Should().ContainKey("HasBringIn");
		rules.SpecialRules["HasBringIn"].Should().Be(true);
	}

	[Fact]
	public void TollboothGame_Metadata_ShouldBeCorrect()
	{
		var game = new TollboothGame();

		game.Name.Should().Be("Tollbooth");
		game.VariantType.Should().Be(VariantType.Stud);
		game.MinimumNumberOfPlayers.Should().Be(2);
		game.MaximumNumberOfPlayers.Should().Be(7);
	}

	[Fact]
	public void TollboothGame_GetGameRules_ShouldReturnSameAsStatic()
	{
		var game = new TollboothGame();
		var fromGame = game.GetGameRules();
		var fromStatic = TollboothRules.CreateGameRules();

		fromGame.GameTypeCode.Should().Be(fromStatic.GameTypeCode);
		fromGame.Phases.Should().HaveCount(fromStatic.Phases.Count);
	}

	[Fact]
	public void Phases_TollboothOffer_EnumExists()
	{
		var phase = Phases.TollboothOffer;
		phase.Should().BeDefined();
	}
}
