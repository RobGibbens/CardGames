#nullable enable

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Features.Games.AvailablePokerGames.v1.Queries.GetAvailablePokerGames;
using CardGames.Poker.Betting;
using CardGames.Poker.Games;
using CardGames.Poker.Games.GameFlow;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CardGames.Poker.Tests.Api.Features.Games.AvailablePokerGames.v1.Queries.GetAvailablePokerGames;

public class GetAvailablePokerGamesQueryHandlerTests
{
	[Fact]
	public async Task Handle_WithUnknownVariant_ReturnsEmptyList()
	{
		using var serviceProvider = CreateServiceProvider();
		var sut = new GetAvailablePokerGamesQueryHandler(serviceProvider.GetRequiredService<HybridCache>());

		var response = await sut.Handle(new GetAvailablePokerGamesQuery("NotARealVariant"), CancellationToken.None);

		response.Should().BeEmpty();
	}

	[Fact]
	public async Task Handle_ConcurrentRequests_ReturnConsistentResults()
	{
		using var serviceProvider = CreateServiceProvider();
		var sut = new GetAvailablePokerGamesQueryHandler(serviceProvider.GetRequiredService<HybridCache>());
		var query = new GetAvailablePokerGamesQuery();

		var results = await Task.WhenAll(Enumerable.Range(0, 20)
			.Select(_ => sut.Handle(query, CancellationToken.None)));

		results.Should().OnlyContain(result => result.Count > 0);
		results.Should().AllSatisfy(result => result.Should().BeEquivalentTo(results[0]));
	}

	[Fact]
	public void CreateGameResponse_WithoutMetadata_ReturnsNull()
	{
		var response = InvokeCreateGameResponse(typeof(GameWithoutMetadata));

		response.Should().BeNull();
	}

	[Fact]
	public void CreateGameResponse_WhenConstructorValidationFails_ReturnsNull()
	{
		var response = InvokeCreateGameResponse(typeof(GameWithFailingValidation));

		response.Should().BeNull();
	}

	[Fact]
	public void CreateGameResponse_WhenRulesAreMissing_DefaultsBettingFlagsToFalse()
	{
		var response = InvokeCreateGameResponse(typeof(GameWithoutRegisteredRules));

		response.Should().NotBeNull();
		response!.Code.Should().Be("TEST_NO_RULES");
		response.Name.Should().Be("Test No Rules");
		response.VariantType.Should().Be(VariantType.HoldEm);
		response.HasAntes.Should().BeFalse();
		response.HasBlinds.Should().BeFalse();
	}

	private static ServiceProvider CreateServiceProvider()
	{
		var services = new ServiceCollection();
		#pragma warning disable EXTEXP0018
		services.AddHybridCache();
		#pragma warning restore EXTEXP0018
		return services.BuildServiceProvider();
	}

	private static GetAvailablePokerGamesResponse? InvokeCreateGameResponse(Type gameType)
	{
		var method = typeof(GetAvailablePokerGamesQueryHandler)
			.GetMethod("CreateGameResponse", BindingFlags.Static | BindingFlags.NonPublic);

		method.Should().NotBeNull("the handler should keep reflection-to-response mapping in one place");

		return (GetAvailablePokerGamesResponse?)method!.Invoke(null, [gameType]);
	}

	private static GameRules CreatePlaceholderRules(string gameTypeCode, string gameTypeName)
	{
		return new GameRules
		{
			GameTypeCode = gameTypeCode,
			GameTypeName = gameTypeName,
			Description = $"Placeholder rules for {gameTypeName}.",
			MinPlayers = 2,
			MaxPlayers = 6,
			Phases =
			[
				new GamePhaseDescriptor
				{
					PhaseId = "Setup",
					Name = "Setup",
					Description = "Placeholder setup phase.",
					Category = "Setup",
					RequiresPlayerAction = false
				}
			],
			CardDealing = new CardDealingConfig
			{
				InitialCards = 2,
				InitialVisibility = CardVisibility.FaceDown
			},
			Betting = new BettingConfig
			{
				HasAntes = false,
				HasBlinds = false,
				BettingRounds = 1,
				Structure = "Fixed Limit"
			},
			Showdown = new ShowdownConfig
			{
				HandRanking = "High"
			}
		};
	}

	private sealed class GameWithoutMetadata : IPokerGame
	{
		public string Name => "Missing Metadata";
		public string Description => "No metadata attribute.";
		public VariantType VariantType => VariantType.Other;
		public int MinimumNumberOfPlayers => 2;
		public int MaximumNumberOfPlayers => 6;

		public GameRules GetGameRules() => CreatePlaceholderRules("NO_METADATA", "Missing Metadata");
	}

	[PokerGameMetadata(
		code: "TEST_INVALID",
		name: "Validation Failure",
		description: "Throws when default constructor arguments are invalid.",
		minimumNumberOfPlayers: 2,
		maximumNumberOfPlayers: 6,
		initialHoleCards: 2,
		initialBoardCards: 0,
		maxCommunityCards: 5,
		maxPlayerCards: 2,
		hasDrawPhase: false,
		maxDiscards: 0,
		wildCardRule: WildCardRule.None,
		bettingStructure: BettingStructure.Blinds)]
	private sealed class GameWithFailingValidation(int seatCount) : IPokerGame
	{
		public string Name => "Validation Failure";
		public string Description => "Throws when default constructor arguments are invalid.";
		public VariantType VariantType => VariantType.Other;
		public int MinimumNumberOfPlayers => 2;
		public int MaximumNumberOfPlayers => 6;

		public GameRules GetGameRules() => CreatePlaceholderRules("TEST_INVALID", "Validation Failure");

		public int SeatCount { get; } = seatCount > 0
			? seatCount
			: throw new InvalidOperationException("seatCount must be positive");
	}

	[PokerGameMetadata(
		code: "TEST_NO_RULES",
		name: "Test No Rules",
		description: "Metadata exists but rules are not registered.",
		minimumNumberOfPlayers: 2,
		maximumNumberOfPlayers: 8,
		initialHoleCards: 2,
		initialBoardCards: 0,
		maxCommunityCards: 5,
		maxPlayerCards: 2,
		hasDrawPhase: false,
		maxDiscards: 0,
		wildCardRule: WildCardRule.None,
		bettingStructure: BettingStructure.Blinds,
		imageName: "test-no-rules.png")]
	private sealed class GameWithoutRegisteredRules : IPokerGame
	{
		public string Name => "Incorrect Name Should Not Leak";
		public string Description => "Incorrect description should not leak.";
		public VariantType VariantType => VariantType.HoldEm;
		public int MinimumNumberOfPlayers => 99;
		public int MaximumNumberOfPlayers => 100;

		public GameRules GetGameRules() => CreatePlaceholderRules("UNUSED", "Unused");
	}
}