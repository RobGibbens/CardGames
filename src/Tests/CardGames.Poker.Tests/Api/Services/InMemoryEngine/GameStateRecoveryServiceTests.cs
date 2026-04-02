using System;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Services.InMemoryEngine;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services.InMemoryEngine;

public class GameStateRecoveryServiceTests
{
    [Fact]
    public async Task ExecuteAsync_Disabled_DoesNotQueryDatabase()
    {
        var gameStateManager = Substitute.For<IGameStateManager>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var options = Options.Create(new InMemoryEngineOptions { Enabled = false });
        var logger = Substitute.For<ILogger<GameStateRecoveryService>>();

        var sut = new GameStateRecoveryService(gameStateManager, scopeFactory, options, logger);

        await sut.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        await sut.StopAsync(CancellationToken.None);

        // Should not attempt to load anything
        await gameStateManager.DidNotReceive()
            .GetOrLoadGameAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        scopeFactory.DidNotReceive().CreateScope();
    }
}
