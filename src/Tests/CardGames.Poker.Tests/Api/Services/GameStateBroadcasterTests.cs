#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Contracts.SignalR;
using CardGames.Poker.Api.Hubs;
using CardGames.Poker.Api.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace CardGames.Poker.Tests.Api.Services;

public class GameStateBroadcasterTests
{
	[Fact]
	public async Task BroadcastGameStateAsync_KingsAndLowsDropOrStay_DelaysTimerUntilDealAnimationCompletes()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(
			gameId,
			"KINGSANDLOWS",
			"DropOrStay",
			3,
			currentPhaseRequiresAction: true,
			seats:
			[
				CreateSeat(0, 5, "Alice"),
				CreateSeat(1, 5, "Bob"),
				CreateSeat(2, 5, "Cara"),
				CreateSeat(3, 5, "Drew")
			]);
		var (sut, tableStateBuilder, actionTimerService, _) = CreateSubject(publicState);
		var expectedStartDelay = TimeSpan.FromMilliseconds(15850);

		await sut.BroadcastGameStateAsync(gameId);

		actionTimerService.Received(1)
			.StartTimer(gameId, -1, IActionTimerService.DefaultTimerDurationSeconds, Arg.Any<Func<Guid, int, Task>?>(), expectedStartDelay);
		await tableStateBuilder.Received(1)
			.BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task BroadcastGameStateAsync_ScrewYourNeighborKeepOrTrade_UsesThirtySecondTimer()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(gameId, "SCREWYOURNEIGHBOR", "KeepOrTrade", 3, currentPhaseRequiresAction: true);
		var (sut, tableStateBuilder, actionTimerService, _) = CreateSubject(publicState);

		await sut.BroadcastGameStateAsync(gameId);

		actionTimerService.Received(1)
			.StartTimer(gameId, 3, 30, Arg.Any<Func<Guid, int, Task>?>(), TimeSpan.Zero);
		await tableStateBuilder.Received(1)
			.BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task BroadcastGameStateAsync_NonScrewYourNeighborAction_UsesDefaultTimer()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(gameId, "FIVECARDDRAW", "DrawPhase", 1);
		var (sut, _, actionTimerService, _) = CreateSubject(publicState);

		await sut.BroadcastGameStateAsync(gameId);

		actionTimerService.Received(1)
			.StartTimer(gameId, 1, IActionTimerService.DefaultTimerDurationSeconds, Arg.Any<Func<Guid, int, Task>?>(), TimeSpan.Zero);
	}

	[Fact]
	public async Task BroadcastGameStateAsync_BobBarkerDrawPhase_UsesSharedTimer()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(gameId, "BOBBARKER", "DrawPhase", 1, currentPhaseRequiresAction: true);
		var (sut, _, actionTimerService, _) = CreateSubject(publicState);

		await sut.BroadcastGameStateAsync(gameId);

		actionTimerService.Received(1)
			.StartTimer(gameId, -1, IActionTimerService.DefaultTimerDurationSeconds, Arg.Any<Func<Guid, int, Task>?>(), TimeSpan.Zero);
	}

	[Fact]
	public async Task BroadcastGameStateAsync_ChipCheckPause_PreservesExistingPauseTimer()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(
			gameId,
			"FIVECARDDRAW",
			"WaitingForPlayers",
			-1,
			isPaused: true,
			chipCheckPause: new ChipCheckPauseStateDto
			{
				IsPaused = true,
				PauseStartedAt = DateTimeOffset.UtcNow,
				PauseEndsAt = DateTimeOffset.UtcNow.AddMinutes(5),
				PotAmountToCover = 10,
				ShortPlayers = []
			});

		var existingTimer = new ActionTimerState
		{
			GameId = gameId,
			PlayerSeatIndex = -1,
			TimerType = ActionTimerType.ChipCheckPause,
			DurationSeconds = 300,
			StartedAtUtc = DateTimeOffset.UtcNow
		};

		var (sut, _, actionTimerService, _) = CreateSubject(publicState);
		actionTimerService.GetTimerState(gameId).Returns(existingTimer);

		await sut.BroadcastGameStateAsync(gameId);

		actionTimerService.DidNotReceiveWithAnyArgs().StopTimer(default);
		actionTimerService.DidNotReceiveWithAnyArgs().StartTimer(default, default, default, default, default);
		actionTimerService.DidNotReceiveWithAnyArgs().StartChipCheckPauseTimer(default, default, default, default);
	}

	[Fact]
	public async Task BroadcastTableToastAsync_SendsToastToGameGroup()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(gameId, "SCREWYOURNEIGHBOR", "KeepOrTrade", 1);
		var (sut, _, _, clientProxy) = CreateSubject(publicState);

		await sut.BroadcastTableToastAsync(new TableToastNotificationDto
		{
			GameId = gameId,
			Message = "Starting new deck"
		});

		await clientProxy.Received(1).SendCoreAsync(
			"TableToastNotification",
			Arg.Is<object?[]>(args =>
				args.Length == 1 &&
				args[0] != null &&
				((TableToastNotificationDto)args[0]!).GameId == gameId &&
				((TableToastNotificationDto)args[0]!).Message == "Starting new deck"),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task BroadcastTableToastAsync_CreatesGameIdScope()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(gameId, "SCREWYOURNEIGHBOR", "KeepOrTrade", 1);
		var logger = new CapturingLogger<GameStateBroadcaster>();
		var (sut, _, _, _) = CreateSubject(publicState, logger);

		await sut.BroadcastTableToastAsync(new TableToastNotificationDto
		{
			GameId = gameId,
			Message = "Starting new deck"
		});

		Assert.Contains(logger.Scopes, scope =>
			scope.TryGetValue("GameId", out var value) && Equals(value, gameId));
	}

	[Fact]
	public async Task BroadcastTableToastAsync_Success_RecordsOkMetric()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(gameId, "SCREWYOURNEIGHBOR", "KeepOrTrade", 1);
		using var serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
		using var listener = CreateCounterListener(out var measurements);
		var telemetry = new BroadcastTelemetry(serviceProvider.GetRequiredService<IMeterFactory>());
		var (sut, _, _, _) = CreateSubject(publicState, telemetry: telemetry);

		await sut.BroadcastTableToastAsync(new TableToastNotificationDto
		{
			GameId = gameId,
			Message = "Starting new deck"
		});

		Assert.Contains(measurements, measurement => HasTags(measurement.Tags, "game", "TableToastNotification", "ok"));
	}

	[Fact]
	public async Task BroadcastTableToastAsync_SendFails_RecordsFailedMetricAndLogsErrorWithoutThrowing()
	{
		var gameId = Guid.NewGuid();
		var publicState = CreateState(gameId, "SCREWYOURNEIGHBOR", "KeepOrTrade", 1);
		using var serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
		using var listener = CreateCounterListener(out var measurements);
		var telemetry = new BroadcastTelemetry(serviceProvider.GetRequiredService<IMeterFactory>());
		var logger = new CapturingLogger<GameStateBroadcaster>();
		var failure = new InvalidOperationException("send failed");
		var (sut, _, _, clientProxy) = CreateSubject(publicState, logger, telemetry);
		clientProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromException(failure));

		var exception = await Record.ExceptionAsync(() => sut.BroadcastTableToastAsync(new TableToastNotificationDto
		{
			GameId = gameId,
			Message = "Starting new deck"
		}));

		Assert.Null(exception);

		Assert.Contains(measurements, measurement => HasTags(measurement.Tags, "game", "TableToastNotification", "failed"));
		Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error && entry.Exception == failure);
	}

	[Fact]
	public async Task BroadcastGameStateAsync_CancelledTokenAndAliceSendFails_StillSendsBobPrivateState()
	{
		var gameId = Guid.NewGuid();
		var aliceId = "alice@example.com";
		var bobId = "bob@example.com";
		var (sut, tableStateBuilder, groupProxy, aliceProxy, bobProxy) = CreateFanOutSubject(gameId, aliceId, bobId);
		aliceProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.FromException(new InvalidOperationException("alice send failed")));

		var exception = await Record.ExceptionAsync(() =>
			sut.BroadcastGameStateAsync(gameId, new CancellationToken(canceled: true)));

		Assert.Null(exception);
		await groupProxy.Received(1).SendCoreAsync(
			"TableStateUpdated",
			Arg.Any<object?[]>(),
			CancellationToken.None);
		await bobProxy.Received(1).SendCoreAsync(
			"PrivateStateUpdated",
			Arg.Any<object?[]>(),
			CancellationToken.None);
		await aliceProxy.Received(1).SendCoreAsync(
			Arg.Any<string>(),
			Arg.Any<object?[]>(),
			CancellationToken.None);
		await tableStateBuilder.Received(1).BuildPublicStateAsync(gameId, CancellationToken.None);
		await tableStateBuilder.Received(1).GetPlayerUserIdsAsync(gameId, CancellationToken.None);
		await tableStateBuilder.Received(1).BuildPrivateStateAsync(gameId, aliceId, CancellationToken.None);
		await tableStateBuilder.Received(1).BuildPrivateStateAsync(gameId, bobId, CancellationToken.None);
	}

	[Fact]
	public async Task BroadcastGameStateAsync_AlicePrivateStateBuildFails_StillSendsBobPrivateState()
	{
		var gameId = Guid.NewGuid();
		var aliceId = "alice@example.com";
		var bobId = "bob@example.com";
		var (sut, tableStateBuilder, _, _, bobProxy) = CreateFanOutSubject(gameId, aliceId, bobId);
		tableStateBuilder.BuildPrivateStateAsync(gameId, aliceId, Arg.Any<CancellationToken>())
			.Returns<Task<PrivateStateDto?>>(_ => throw new InvalidOperationException("alice private state failed"));

		var exception = await Record.ExceptionAsync(() =>
			sut.BroadcastGameStateAsync(gameId, new CancellationToken(canceled: true)));

		Assert.Null(exception);
		await tableStateBuilder.Received(1).BuildPrivateStateAsync(gameId, aliceId, CancellationToken.None);
		await tableStateBuilder.Received(1).BuildPrivateStateAsync(gameId, bobId, CancellationToken.None);
		await bobProxy.Received(1).SendCoreAsync(
			"PrivateStateUpdated",
			Arg.Any<object?[]>(),
			CancellationToken.None);
	}

	[Fact]
	public async Task BroadcastPlayerJoinedAsync_KnownConnections_ExcludesThoseConnectionIds()
	{
		var gameId = Guid.NewGuid();
		var playerId = Guid.NewGuid();
		var excludedProxy = Substitute.For<IClientProxy>();
		excludedProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var hubClients = Substitute.For<IHubClients>();
		hubClients.GroupExcept(
				$"game:{gameId}",
				Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "conn-alice" })))
			.Returns(excludedProxy);

		var registry = Substitute.For<IGameConnectionRegistry>();
		registry.GetConnectionIds(gameId, playerId).Returns(new[] { "conn-alice" });

		var sut = CreateJoinedSubject(hubClients, registry);
		var exception = await Record.ExceptionAsync(() =>
			sut.BroadcastPlayerJoinedAsync(gameId, "Alice", 2, true, playerId));

		Assert.Null(exception);
		hubClients.Received(1).GroupExcept(
			$"game:{gameId}",
			Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "conn-alice" })));
		hubClients.DidNotReceive().Group(Arg.Any<string>());
		hubClients.DidNotReceive().GroupExcept(
			Arg.Any<string>(),
			Arg.Is<IReadOnlyList<string>>(ids => ids.Contains("Alice")));
		await excludedProxy.Received(1).SendCoreAsync(
			"PlayerJoined",
			Arg.Is<object?[]>(args => IsPlayerJoined(args, playerId, "Alice")),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task BroadcastPlayerJoinedAsync_NoKnownConnections_SendsToTheGroup()
	{
		var gameId = Guid.NewGuid();
		var playerId = Guid.NewGuid();
		var groupProxy = Substitute.For<IClientProxy>();
		groupProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var hubClients = Substitute.For<IHubClients>();
		hubClients.Group($"game:{gameId}").Returns(groupProxy);

		var registry = Substitute.For<IGameConnectionRegistry>();
		registry.GetConnectionIds(gameId, playerId).Returns(Array.Empty<string>());

		var sut = CreateJoinedSubject(hubClients, registry);
		var exception = await Record.ExceptionAsync(() =>
			sut.BroadcastPlayerJoinedAsync(gameId, "Alice", 2, false, playerId));

		Assert.Null(exception);
		hubClients.Received(1).Group($"game:{gameId}");
		hubClients.DidNotReceive().GroupExcept(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>());
		await groupProxy.Received(1).SendCoreAsync(
			"PlayerJoined",
			Arg.Is<object?[]>(args => IsPlayerJoined(args, playerId, "Alice")),
			Arg.Any<CancellationToken>());
	}

	private static (
		GameStateBroadcaster Sut,
		ITableStateBuilder TableStateBuilder,
		IClientProxy GroupProxy,
		IClientProxy AliceProxy,
		IClientProxy BobProxy) CreateFanOutSubject(Guid gameId, string aliceId, string bobId)
	{
		var publicState = CreateState(gameId, "FIVECARDDRAW", "WaitingForPlayers", -1);
		var tableStateBuilder = Substitute.For<ITableStateBuilder>();
		tableStateBuilder.BuildPublicStateAsync(gameId, Arg.Any<CancellationToken>())
			.Returns(publicState);
		tableStateBuilder.GetPlayerUserIdsAsync(gameId, Arg.Any<CancellationToken>())
			.Returns(new[] { aliceId, bobId });
		tableStateBuilder.BuildPrivateStateAsync(gameId, Arg.Any<string>(), Arg.Any<CancellationToken>())
			.Returns(call => new PrivateStateDto
			{
				GameId = gameId,
				PlayerName = call.ArgAt<string>(1),
				Hand = []
			});

		var groupProxy = Substitute.For<IClientProxy>();
		var aliceProxy = Substitute.For<IClientProxy>();
		var bobProxy = Substitute.For<IClientProxy>();
		groupProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);
		aliceProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);
		bobProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var hubClients = Substitute.For<IHubClients>();
		hubClients.Group(Arg.Any<string>()).Returns(groupProxy);
		hubClients.User(aliceId).Returns(aliceProxy);
		hubClients.User(bobId).Returns(bobProxy);

		var hubContext = Substitute.For<IHubContext<GameHub>>();
		hubContext.Clients.Returns(hubClients);

		var actionTimerService = Substitute.For<IActionTimerService>();
		actionTimerService.GetTimerState(gameId).Returns((ActionTimerState?)null);

		var sut = new GameStateBroadcaster(
			hubContext,
			tableStateBuilder,
			actionTimerService,
			Substitute.For<IAutoActionService>(),
			Substitute.For<ILogger<GameStateBroadcaster>>(),
			CreateTelemetry(),
			EmptyConnectionRegistry());

		return (sut, tableStateBuilder, groupProxy, aliceProxy, bobProxy);
	}

	private static (GameStateBroadcaster Sut, ITableStateBuilder TableStateBuilder, IActionTimerService ActionTimerService, IClientProxy ClientProxy) CreateSubject(
		TableStatePublicDto publicState,
		ILogger<GameStateBroadcaster>? logger = null,
		BroadcastTelemetry? telemetry = null)
	{
		var tableStateBuilder = Substitute.For<ITableStateBuilder>();
		tableStateBuilder.BuildPublicStateAsync(publicState.GameId, Arg.Any<CancellationToken>())
			.Returns(publicState);
		tableStateBuilder.GetPlayerUserIdsAsync(publicState.GameId, Arg.Any<CancellationToken>())
			.Returns(Array.Empty<string>());

		var actionTimerService = Substitute.For<IActionTimerService>();
		actionTimerService.GetTimerState(publicState.GameId).Returns((ActionTimerState?)null);
		actionTimerService.IsTimerActive(publicState.GameId).Returns(false);

		var clientProxy = Substitute.For<IClientProxy>();
		clientProxy.SendCoreAsync(Arg.Any<string>(), Arg.Any<object?[]>(), Arg.Any<CancellationToken>())
			.Returns(Task.CompletedTask);

		var hubClients = Substitute.For<IHubClients>();
		hubClients.Group(Arg.Any<string>()).Returns(clientProxy);

		var hubContext = Substitute.For<IHubContext<GameHub>>();
		hubContext.Clients.Returns(hubClients);

		var sut = new GameStateBroadcaster(
			hubContext,
			tableStateBuilder,
			actionTimerService,
			Substitute.For<IAutoActionService>(),
			logger ?? Substitute.For<ILogger<GameStateBroadcaster>>(),
			telemetry ?? CreateTelemetry(),
			EmptyConnectionRegistry());

		return (sut, tableStateBuilder, actionTimerService, clientProxy);
	}

	private static bool IsPlayerJoined(object?[] args, Guid playerId, string playerName)
	{
		return args.Length == 1 &&
		       args[0] is PlayerJoinedDto dto &&
		       dto.PlayerId == playerId &&
		       dto.PlayerName == playerName;
	}

	private static GameStateBroadcaster CreateJoinedSubject(IHubClients hubClients, IGameConnectionRegistry registry)
	{
		var hubContext = Substitute.For<IHubContext<GameHub>>();
		hubContext.Clients.Returns(hubClients);

		return new GameStateBroadcaster(
			hubContext,
			Substitute.For<ITableStateBuilder>(),
			Substitute.For<IActionTimerService>(),
			Substitute.For<IAutoActionService>(),
			Substitute.For<ILogger<GameStateBroadcaster>>(),
			CreateTelemetry(),
			registry);
	}

	private static IGameConnectionRegistry EmptyConnectionRegistry()
	{
		var registry = Substitute.For<IGameConnectionRegistry>();
		registry.GetConnectionIds(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(Array.Empty<string>());
		return registry;
	}

	private static BroadcastTelemetry CreateTelemetry()
	{
		var serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
		return new BroadcastTelemetry(serviceProvider.GetRequiredService<IMeterFactory>());
	}

	private static MeterListener CreateCounterListener(out List<(long Value, KeyValuePair<string, object?>[] Tags)> measurements)
	{
		measurements = [];
		var capturedMeasurements = measurements;
		var listener = new MeterListener();
		listener.InstrumentPublished = (instrument, meterListener) =>
		{
			if (instrument.Meter.Name == BroadcastTelemetry.MeterName &&
				instrument.Name == "realtime_broadcasts_total")
			{
				meterListener.EnableMeasurementEvents(instrument);
			}
		};
		listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
		{
			capturedMeasurements.Add((value, tags.ToArray()));
		});
		listener.Start();
		return listener;
	}

	private static bool HasTags(KeyValuePair<string, object?>[] tags, string hub, string eventName, string outcome)
	{
		return tags.Any(tag => tag.Key == "hub" && Equals(tag.Value, hub)) &&
			tags.Any(tag => tag.Key == "event" && Equals(tag.Value, eventName)) &&
			tags.Any(tag => tag.Key == "outcome" && Equals(tag.Value, outcome));
	}

	private sealed class CapturingLogger<T> : ILogger<T>
	{
		public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];
		public List<IReadOnlyDictionary<string, object>> Scopes { get; } = [];
		public IDisposable BeginScope<TState>(TState state) where TState : notnull
		{
			if (state is IEnumerable<KeyValuePair<string, object>> pairs)
			{
				Scopes.Add(pairs.ToDictionary(pair => pair.Key, pair => pair.Value));
			}

			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			Entries.Add((logLevel, exception));
		}

		private sealed class NullScope : IDisposable
		{
			public static readonly NullScope Instance = new();
			public void Dispose() { }
		}
	}

	private static TableStatePublicDto CreateState(
		Guid gameId,
		string gameTypeCode,
		string currentPhase,
		int currentActorSeatIndex,
		bool currentPhaseRequiresAction = false,
		SeatPublicDto[]? seats = null,
		bool isPaused = false,
		ChipCheckPauseStateDto? chipCheckPause = null)
	{
		return new TableStatePublicDto
		{
			GameId = gameId,
			GameTypeCode = gameTypeCode,
			CurrentPhase = currentPhase,
			CurrentActorSeatIndex = currentActorSeatIndex,
			CurrentPhaseRequiresAction = currentPhaseRequiresAction,
			Seats = seats ?? [],
			IsPaused = isPaused,
			ChipCheckPause = chipCheckPause
		};
	}

	private static SeatPublicDto CreateSeat(int seatIndex, int cardCount, string playerName)
	{
		return new SeatPublicDto
		{
			SeatIndex = seatIndex,
			IsOccupied = true,
			IsSittingOut = false,
			IsFolded = false,
			PlayerName = playerName,
			Cards = Enumerable.Range(0, cardCount)
				.Select(index => new CardPublicDto
				{
					IsFaceUp = true,
					Rank = ((index % 9) + 2).ToString(),
					Suit = "Hearts"
				})
				.ToArray()
		};
	}
}
