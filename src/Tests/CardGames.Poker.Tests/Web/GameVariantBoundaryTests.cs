using System;
using System.Collections.Generic;
using System.Linq;
using CardGames.Poker.Api.Clients;
using CardGames.Poker.Api.Games;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Web;

/// <summary>
/// Enforces the cross-layer variant boundary documented in <c>docs/GameVariantBoundary.md</c>.
/// These tests make <em>partial onboarding</em> fail loudly: if a variant is added to the
/// domain/metadata layer (discovered by <see cref="PokerGameMetadataRegistry"/>) but not wired
/// into the active web router (<see cref="GameApiRouter"/>), or vice versa, a test here fails.
/// </summary>
public class GameVariantBoundaryTests
{
    /// <summary>
    /// Variants that intentionally drive play through their own action families instead of the
    /// standard betting dispatch table. Adding to this set is a deliberate, reviewed decision —
    /// it should match the "special-action variants" section of <c>docs/GameVariantBoundary.md</c>.
    /// </summary>
    private static readonly IReadOnlySet<string> StandardBettingExemptVariants =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            PokerGameMetadataRegistry.KingsAndLowsCode,      // no betting: drop-or-stay + draw + ack-pot-match
            PokerGameMetadataRegistry.ScrewYourNeighborCode, // uses keep-or-trade
            PokerGameMetadataRegistry.InBetweenCode,         // uses its own ace-choice / place-bet methods
        };

    [Fact]
    public void Every_domain_variant_is_wired_into_the_active_router_or_explicitly_exempt()
    {
        var router = CreateRouter();
        var bettingCodes = ToSet(router.SupportedBettingGameCodes);

        var missing = PokerGameMetadataRegistry.GetAllGameTypeCodes()
            .Where(code => !bettingCodes.Contains(code))
            .Where(code => !StandardBettingExemptVariants.Contains(code))
            .ToList();

        missing.Should().BeEmpty(
            "every domain variant must be wired into the active web router's betting table " +
            "(GameApiRouter) or explicitly listed as a special-action variant in " +
            "StandardBettingExemptVariants. See docs/GameVariantBoundary.md.");
    }

    [Fact]
    public void Every_betting_route_targets_a_registered_domain_variant()
    {
        var router = CreateRouter();

        AssertNoStaleRoutes(router.SupportedBettingGameCodes, "betting");
    }

    [Fact]
    public void Every_draw_route_targets_a_registered_domain_variant()
    {
        var router = CreateRouter();

        AssertNoStaleRoutes(router.SupportedDrawGameCodes, "draw");
    }

    [Fact]
    public void Every_optional_action_route_targets_a_registered_domain_variant()
    {
        var router = CreateRouter();

        AssertNoStaleRoutes(router.SupportedDropOrStayGameCodes, "drop-or-stay");
        AssertNoStaleRoutes(router.SupportedKeepOrTradeGameCodes, "keep-or-trade");
        AssertNoStaleRoutes(router.SupportedBuyCardGameCodes, "buy-card");
        AssertNoStaleRoutes(router.SupportedAcknowledgePotMatchGameCodes, "acknowledge-pot-match");
    }

    [Fact]
    public void Betting_exemptions_are_registered_variants_that_genuinely_have_no_betting_route()
    {
        var router = CreateRouter();
        var bettingCodes = ToSet(router.SupportedBettingGameCodes);

        foreach (var exempt in StandardBettingExemptVariants)
        {
            PokerGameMetadataRegistry.IsRegistered(exempt).Should().BeTrue(
                $"exempt variant '{exempt}' must be a registered domain variant; remove it from " +
                "StandardBettingExemptVariants if it no longer exists.");

            bettingCodes.Should().NotContain(exempt,
                $"exempt variant '{exempt}' actually has a betting route now, so it is no longer a " +
                "special-action variant; remove it from StandardBettingExemptVariants.");
        }
    }

    private static void AssertNoStaleRoutes(IEnumerable<string> routeCodes, string actionFamily)
    {
        var stale = routeCodes
            .Where(code => !PokerGameMetadataRegistry.IsRegistered(code))
            .ToList();

        stale.Should().BeEmpty(
            $"every {actionFamily} route in the active web router must target a game code that is " +
            "registered in the domain metadata registry; a stale entry usually means a typo or a " +
            "removed variant. See docs/GameVariantBoundary.md.");
    }

    private static HashSet<string> ToSet(IEnumerable<string> codes) =>
        new(codes, StringComparer.OrdinalIgnoreCase);

    private static GameApiRouter CreateRouter() => new(
        Substitute.For<IFiveCardDrawApi>(),
        Substitute.For<ITwosJacksManWithTheAxeApi>(),
        Substitute.For<IKingsAndLowsApi>(),
        Substitute.For<ISevenCardStudApi>(),
        Substitute.For<IPairPressureApi>(),
        Substitute.For<IGoodBadUglyApi>(),
        Substitute.For<IBaseballApi>(),
        Substitute.For<IFollowTheQueenApi>(),
        Substitute.For<IHoldEmApi>(),
        Substitute.For<IGamesApi>(),
        Substitute.For<IScrewYourNeighborApi>(),
        Substitute.For<ITollboothApi>(),
        Substitute.For<IInBetweenApi>());
}
