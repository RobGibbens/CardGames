using System;
using System.Linq;
using System.Threading.Tasks;
using CardGames.Poker.Web.Services;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TablePlayToastStateTests
{
    [Fact]
    public async Task ShowAsync_Adds_Message_With_Given_Text_And_Type()
    {
        var state = new TablePlayToastState();

        await state.ShowAsync("Welcome back!", "success");

        state.Messages.Should().ContainSingle();
        state.Messages[0].Message.Should().Be("Welcome back!");
        state.Messages[0].Type.Should().Be("success");
    }

    [Fact]
    public async Task ShowAsync_Defaults_To_Info_Type()
    {
        var state = new TablePlayToastState();

        await state.ShowAsync("Heads up");

        state.Messages[0].Type.Should().Be("info");
    }

    [Fact]
    public async Task ShowAsync_Raises_OnChanged_When_Message_Added()
    {
        var state = new TablePlayToastState();
        var changes = 0;
        state.OnChanged += () => changes++;

        await state.ShowAsync("Hi", "info");

        changes.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task ShowAsync_Preserves_Insertion_Order_For_Multiple_Messages()
    {
        var state = new TablePlayToastState();

        await state.ShowAsync("first", "info");
        await state.ShowAsync("second", "error");

        state.Messages.Select(m => m.Message).Should().ContainInOrder("first", "second");
    }

    [Fact]
    public async Task ShowAsync_Auto_Dismisses_Message_After_Duration()
    {
        var state = new TablePlayToastState();
        var dismissed = new TaskCompletionSource();
        state.OnChanged += () =>
        {
            if (state.Messages.Count == 0)
            {
                dismissed.TrySetResult();
            }
        };

        await state.ShowAsync("temporary", "info", durationMs: 50);

        await dismissed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        state.Messages.Should().BeEmpty();
    }
}
