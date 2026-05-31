using CardGames.Poker.Web.Services.TableActions;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TableRefreshPolicyTests
{
    [Theory]
    [InlineData(TableInteraction.InitialLoad)]
    [InlineData(TableInteraction.JoinRequestApproved)]
    [InlineData(TableInteraction.ManualStartFallback)]
    public void Bootstrap_And_Fallback_Interactions_Use_Full_Refetch(TableInteraction interaction)
    {
        TableRefreshPolicy.ResolveRefreshKind(interaction).Should().Be(TableRefreshKind.Full);
    }

    [Theory]
    [InlineData(TableInteraction.BettingAction)]
    [InlineData(TableInteraction.DrawDiscardAction)]
    [InlineData(TableInteraction.SpecialVariantDecision)]
    [InlineData(TableInteraction.AddChips)]
    [InlineData(TableInteraction.SitOutToggle)]
    public void Gameplay_Actions_Wait_For_Hub_Push(TableInteraction interaction)
    {
        TableRefreshPolicy.ResolveRefreshKind(interaction).Should().Be(TableRefreshKind.HubDriven);
    }

    [Fact]
    public void TableSettingsPush_Uses_Focused_Slice_Refetch()
    {
        TableRefreshPolicy.ResolveRefreshKind(TableInteraction.TableSettingsPush)
            .Should().Be(TableRefreshKind.Slice);
    }

    [Theory]
    [InlineData(TableInteraction.OddsVisibilityToggle)]
    [InlineData(TableInteraction.LeaveTable)]
    public void Presentational_And_Navigation_Interactions_Stay_Local(TableInteraction interaction)
    {
        TableRefreshPolicy.ResolveRefreshKind(interaction).Should().Be(TableRefreshKind.LocalOnly);
    }

    [Fact]
    public void Every_Interaction_Maps_To_A_Defined_Refresh_Kind()
    {
        foreach (TableInteraction interaction in System.Enum.GetValues<TableInteraction>())
        {
            var kind = TableRefreshPolicy.ResolveRefreshKind(interaction);

            System.Enum.IsDefined(kind).Should().BeTrue(
                "interaction {0} must map to a defined TableRefreshKind", interaction);
        }
    }
}
