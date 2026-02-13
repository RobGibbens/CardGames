using CardGames.Poker.Api.Services;
using CardGames.Poker.Api.Data;
using CardGames.Poker.Api.Data.Entities;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessBettingAction;
using CardGames.Poker.Api.Features.Games.FiveCardDraw.v1.Commands.ProcessDraw;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DrawCards;
using CardGames.Poker.Api.Features.Games.KingsAndLows.v1.Commands.DropOrStay;
using CardGames.Poker.Api.GameFlow;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Xunit;
using OneOf;
using EntityBettingActionType = CardGames.Poker.Api.Data.Entities.BettingActionType;

namespace CardGames.IntegrationTests.Services;

public class AutoActionServiceTests : IDisposable
{
    private readonly FakeMediator _mediator;
    private readonly CardsDbContext _dbContext;
    private readonly AutoActionService _service;
    private readonly FakeLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public AutoActionServiceTests()
    {
        _mediator = new FakeMediator();
        
        var options = new DbContextOptionsBuilder<CardsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;
            
        _dbContext = new CardsDbContext(options);
        
        _logger = new FakeLogger();
        
        _scopeFactory = new FakeServiceScopeFactory(_dbContext, _mediator);
        _service = new AutoActionService(_scopeFactory, _logger);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task PerformAutoActionAsync_GameNotFound_LogsWarning()
    {
        await _service.PerformAutoActionAsync(Guid.NewGuid(), 1);
        _logger.WarningLogs.Should().Contain(l => l.Contains("not found for auto-action"));
    }

    [Fact]
    public async Task PerformAutoActionAsync_PlayerNotFound_DoesNothing()
    {
        var game = new Game
        {
            Id = Guid.NewGuid(),
            CurrentPhase = "FirstBettingRound",
            GameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" }
        };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(game.Id, 1);

        _mediator.SentCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task PerformAutoActionAsync_GlobalTimer_DropOrStay_CallsForUndecidedPlayers()
    {
        var gameId = Guid.NewGuid();
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "DropOrStay",
            GameType = new GameType { Code = "KINGSANDLOWS", Name = "Kings and Lows" }
        };
        
        var undecidedPlayer = new GamePlayer 
        { 
            GameId = gameId, 
            PlayerId = Guid.NewGuid(),
            SeatPosition = 1,
            Status = GamePlayerStatus.Active,
            HasFolded = false,
            DropOrStayDecision = DropOrStayDecision.Undecided
        };
        
        var decidedPlayer = new GamePlayer 
        { 
            GameId = gameId, 
            PlayerId = Guid.NewGuid(),
            SeatPosition = 2,
            Status = GamePlayerStatus.Active,
            HasFolded = false,
            DropOrStayDecision = DropOrStayDecision.Stay
        };

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.AddRange(undecidedPlayer, decidedPlayer);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, -1);

        _mediator.SentCommands.Should().ContainSingle(c => c is DropOrStayCommand);
        var cmd = _mediator.SentCommands.OfType<DropOrStayCommand>().First();
        cmd.PlayerId.Should().Be(undecidedPlayer.PlayerId);
        cmd.Decision.Should().Be("Drop");
    }
    
    [Fact]
    public async Task PerformAutoActionAsync_GlobalTimer_OtherPhase_DoesNothing()
    {
        var gameId = Guid.NewGuid();
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "FirstBettingRound",
            GameType = new GameType { Code = "KINGSANDLOWS", Name = "Kings and Lows" }
        };
        _dbContext.Games.Add(game);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, -1);

        _mediator.SentCommands.Should().BeEmpty();
    }

    [Fact]
    public async Task PerformAutoActionAsync_Betting_CanCheck_Checks()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "FirstBettingRound",
            GameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            CurrentBet = 50,
            Status = GamePlayerStatus.Active
        };
        
        var opponent = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = 2,
            CurrentBet = 50,
            Status = GamePlayerStatus.Active
        };

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.AddRange(player, opponent);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _mediator.SentCommands.Should().ContainSingle(c => c is ProcessBettingActionCommand);
        var cmd = _mediator.SentCommands.OfType<ProcessBettingActionCommand>().First();
        cmd.ActionType.Should().Be(EntityBettingActionType.Check);
    }
    
    [Fact]
    public async Task PerformAutoActionAsync_Betting_CannotCheck_Folds()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "FirstBettingRound",
            GameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            CurrentBet = 20,
            Status = GamePlayerStatus.Active
        };
        
        var opponent = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = 2,
            CurrentBet = 50,
            Status = GamePlayerStatus.Active
        };

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.AddRange(player, opponent);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _mediator.SentCommands.Should().ContainSingle(c => c is ProcessBettingActionCommand);
        var cmd = _mediator.SentCommands.OfType<ProcessBettingActionCommand>().First();
        cmd.ActionType.Should().Be(EntityBettingActionType.Fold);
    }

    [Fact]
    public async Task PerformAutoActionAsync_Draw_FiveCardDraw_StandsPat()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "DrawPhase",
            GameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            Status = GamePlayerStatus.Active
        };

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.Add(player);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _mediator.SentCommands.Should().ContainSingle(c => c is ProcessDrawCommand);
        var cmd = _mediator.SentCommands.OfType<ProcessDrawCommand>().First();
        cmd.DiscardIndices.Should().BeEmpty();
    }
    
    [Fact]
    public async Task PerformAutoActionAsync_Draw_KingsAndLows_StandsPat()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "DrawPhase",
            GameType = new GameType { Code = "KINGSANDLOWS", Name = "Kings and Lows" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            Status = GamePlayerStatus.Active
        };

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.Add(player);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _mediator.SentCommands.Should().ContainSingle(c => c is DrawCardsCommand);
        var cmd = _mediator.SentCommands.OfType<DrawCardsCommand>().First();
        cmd.DiscardIndices.Should().BeEmpty();
        cmd.PlayerId.Should().Be(player.PlayerId);
    }

    [Fact]
    public async Task PerformAutoActionAsync_DropOrStay_KingsAndLows_Drops()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "DropOrStay",
            GameType = new GameType { Code = "KINGSANDLOWS", Name = "Kings and Lows" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            Status = GamePlayerStatus.Active
        };

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.Add(player);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _mediator.SentCommands.Should().ContainSingle(c => c is DropOrStayCommand);
        var cmd = _mediator.SentCommands.OfType<DropOrStayCommand>().First();
        cmd.Decision.Should().Be("Drop");
        cmd.PlayerId.Should().Be(player.PlayerId);
    }

    [Fact]
    public async Task PerformAutoActionAsync_DropOrStay_OtherGame_LogsDebug()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "DropOrStay",
            GameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            Status = GamePlayerStatus.Active
        };

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.Add(player);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _mediator.SentCommands.Should().BeEmpty();
    }
    
    [Fact]
    public async Task PerformAutoActionAsync_UnknownPhase_LogsDebug()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "UnknownPhase",
            GameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            Status = GamePlayerStatus.Active
        };

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.Add(player);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _mediator.SentCommands.Should().BeEmpty();
    }
    
    [Fact]
    public async Task PerformAutoActionAsync_MediatorError_DoesNotThrowAndSendsCommand()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "FirstBettingRound",
            GameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            CurrentBet = 50,
            Status = GamePlayerStatus.Active
        };
        
        _mediator.ForceError = true;

        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.Add(player);
        await _dbContext.SaveChangesAsync();

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _mediator.SentCommands.Should().ContainSingle(c => c is ProcessBettingActionCommand);
        _logger.ErrorLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task PerformAutoActionAsync_Exception_LogsError()
    {
        var gameId = Guid.NewGuid();
        var playerSeat = 1;
        var game = new Game
        {
            Id = gameId,
            CurrentPhase = "FirstBettingRound",
            GameType = new GameType { Code = "FIVECARDDRAW", Name = "Five Card Draw" }
        };
        
        var player = new GamePlayer
        {
            GameId = gameId,
            PlayerId = Guid.NewGuid(),
            SeatPosition = playerSeat,
            CurrentBet = 50,
            Status = GamePlayerStatus.Active
        };
        
        _dbContext.Games.Add(game);
        _dbContext.GamePlayers.Add(player);
        await _dbContext.SaveChangesAsync();
        
        _mediator.ForceException = true;

        await _service.PerformAutoActionAsync(gameId, playerSeat);

        _logger.ErrorLogs.Should().Contain(l => l.Contains("Error performing auto-betting action"));
    }

    private class FakeMediator : IMediator
    {
        public List<object> SentCommands { get; } = new();
        public bool ForceError { get; set; }
        public bool ForceException { get; set; }

        public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (ForceException)
                throw new Exception("Simulated mediator exception");

            SentCommands.Add(request);

            if (request is ProcessBettingActionCommand bettingCmd)
            {
                if (ForceError) 
                {
                    var error = new ProcessBettingActionError { Message = "Simulated error", Code = default };
                    return (TResponse)(object)OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>.FromT1(error);
                }
                var success = new ProcessBettingActionSuccessful 
                { 
                    GameId = bettingCmd.GameId, 
                    RoundComplete = true,
                    CurrentPhase = "NextPhase",
                    Action = new BettingActionResult { PlayerName = "TestPlayer", ActionType = bettingCmd.ActionType, Amount = bettingCmd.Amount, ChipStackAfter = 1000 }
                };
                return (TResponse)(object)OneOf<ProcessBettingActionSuccessful, ProcessBettingActionError>.FromT0(success);
            }
            
            if (request is ProcessDrawCommand drawCmd)
            {
                if (ForceError) 
                {
                    var error = new ProcessDrawError { Message = "Simulated error", Code = default };
                    return (TResponse)(object)OneOf<ProcessDrawSuccessful, ProcessDrawError>.FromT1(error);
                }
                var success = new ProcessDrawSuccessful 
                { 
                     GameId = drawCmd.GameId, 
                     PlayerName = "TestPlayer",
                     PlayerSeatIndex = 0,
                     NewCards = [],
                     DiscardedCards = [],
                     CurrentPhase = "NextPhase",
                     NextDrawPlayerIndex = -1,
                     DrawComplete = true 
                };
                return (TResponse)(object)OneOf<ProcessDrawSuccessful, ProcessDrawError>.FromT0(success);
            }
            
            if (request is DrawCardsCommand drawCardsCmd)
            {
                if (ForceError)
                {
                     return (TResponse)(object)OneOf<DrawCardsSuccessful, DrawCardsError>.FromT1(new DrawCardsError { Message = "Simulated Error", Code = default });
                }
                var success = new DrawCardsSuccessful 
                { 
                    GameId = drawCardsCmd.GameId, 
                    PlayerId = drawCardsCmd.PlayerId,
                    CardsDiscarded = 0,
                    CardsDrawn = 0,
                    DiscardedCards = [],
                    NewCards = [],
                    DrawPhaseComplete = true
                };
                 return (TResponse)(object)OneOf<DrawCardsSuccessful, DrawCardsError>.FromT0(success);
            }
            
            if (request is DropOrStayCommand dropCmd)
            {
                if (ForceError) return (TResponse)(object)OneOf<DropOrStaySuccessful, DropOrStayError>.FromT1(new DropOrStayError { Message = "Simulated Error", Code = default });
                var success = new DropOrStaySuccessful 
                { 
                    GameId = dropCmd.GameId, 
                    PlayerId = dropCmd.PlayerId, 
                    Decision = dropCmd.Decision, 
                    AllPlayersDecided = true,
                    StayingCount = 0,
                    DroppedCount = 1
                };
                return (TResponse)(object)OneOf<DropOrStaySuccessful, DropOrStayError>.FromT0(success);
            }

            return default!;
        }

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        {
            throw new NotImplementedException();
        }

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
             throw new NotImplementedException();
        }
        
        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
             throw new NotImplementedException();
        }
    }
    
    private class FakeServiceScopeFactory : IServiceScopeFactory
    {
        private readonly CardsDbContext _dbContext;
        private readonly IMediator _mediator;
        private readonly IGameFlowHandlerFactory _gameFlowHandlerFactory;

        public FakeServiceScopeFactory(CardsDbContext dbContext, IMediator mediator)
        {
            _dbContext = dbContext;
            _mediator = mediator;
            _gameFlowHandlerFactory = new GameFlowHandlerFactory();
        }

        public IServiceScope CreateScope()
        {
            return new FakeServiceScope(_dbContext, _mediator, _gameFlowHandlerFactory);
        }
    }

    private class FakeServiceScope : IServiceScope
    {
        public FakeServiceScope(CardsDbContext dbContext, IMediator mediator, IGameFlowHandlerFactory gameFlowHandlerFactory)
        {
            ServiceProvider = new FakeServiceProvider(dbContext, mediator, gameFlowHandlerFactory);
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }

    private class FakeServiceProvider : IServiceProvider
    {
        private readonly CardsDbContext _dbContext;
        private readonly IMediator _mediator;
        private readonly IGameFlowHandlerFactory _gameFlowHandlerFactory;

        public FakeServiceProvider(CardsDbContext dbContext, IMediator mediator, IGameFlowHandlerFactory gameFlowHandlerFactory)
        {
            _dbContext = dbContext;
            _mediator = mediator;
            _gameFlowHandlerFactory = gameFlowHandlerFactory;
        }

        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(CardsDbContext))
                return _dbContext;
            if (serviceType == typeof(IMediator))
                return _mediator;
            if (serviceType == typeof(IGameFlowHandlerFactory))
                return _gameFlowHandlerFactory;
            return null;
        }
    }

    private class FakeLogger : ILogger<AutoActionService>
    {
        public List<string> InformationLogs { get; } = new();
        public List<string> WarningLogs { get; } = new();
        public List<string> ErrorLogs { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            switch (logLevel)
            {
                case LogLevel.Information:
                    InformationLogs.Add(message);
                    break;
                case LogLevel.Warning:
                    WarningLogs.Add(message);
                    break;
                case LogLevel.Error:
                    ErrorLogs.Add(message);
                    break;
            }
        }
    }
}
