#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Infrastructure.PipelineBehaviors;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CardGames.Poker.Tests.Api.Infrastructure.PipelineBehaviors;

public class LoggingPipelineBehaviorTests
{
	private sealed record SensitiveRequest(string Password, string Email) : IRequest<string>;
	private sealed record GameActionRequest(Guid GameId, Guid UserId, int HandNumber) : IRequest<string>;

	[Fact]
	public async Task Handle_DoesNotLogRequestPropertyValues()
	{
		var logger = new CapturingLogger<LoggingPipelineBehavior<SensitiveRequest, string>>();
		var behavior = new LoggingPipelineBehavior<SensitiveRequest, string>(logger);
		var request = new SensitiveRequest("super-secret-token", "player@example.com");

		await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

		var allText = logger.AllRenderedText();
		allText.Should().NotContain("super-secret-token");
		allText.Should().NotContain("player@example.com");
	}

	[Fact]
	public async Task Handle_LogsRequestTypeScopeAndSuccess()
	{
		var logger = new CapturingLogger<LoggingPipelineBehavior<SensitiveRequest, string>>();
		var behavior = new LoggingPipelineBehavior<SensitiveRequest, string>(logger);
		var request = new SensitiveRequest("pw", "e");

		await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

		logger.Scopes.Should().ContainSingle()
			.Which.Should().ContainKey("RequestType")
			.WhoseValue.Should().Be(nameof(SensitiveRequest));

		logger.Entries.Should().Contain(e =>
			e.Level == LogLevel.Information && e.Message.Contains("Handled"));
	}

	[Fact]
	public async Task Handle_IncludesStandardBusinessIdentifiersInScope()
	{
		var logger = new CapturingLogger<LoggingPipelineBehavior<GameActionRequest, string>>();
		var behavior = new LoggingPipelineBehavior<GameActionRequest, string>(logger);
		var request = new GameActionRequest(Guid.NewGuid(), Guid.NewGuid(), 12);

		await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

		var scope = logger.Scopes.Should().ContainSingle().Which;
		scope.Should().ContainKey("RequestType").WhoseValue.Should().Be(nameof(GameActionRequest));
		scope.Should().ContainKey("GameId").WhoseValue.Should().Be(request.GameId);
		scope.Should().ContainKey("UserId").WhoseValue.Should().Be(request.UserId);
		scope.Should().ContainKey("HandNumber").WhoseValue.Should().Be(request.HandNumber);
	}

	[Fact]
	public async Task Handle_LogsErrorWithRequestTypeAndRethrows()
	{
		var logger = new CapturingLogger<LoggingPipelineBehavior<SensitiveRequest, string>>();
		var behavior = new LoggingPipelineBehavior<SensitiveRequest, string>(logger);
		var request = new SensitiveRequest("pw", "e");
		var failure = new InvalidOperationException("boom");

		var act = async () => await behavior.Handle(
			request,
			_ => Task.FromException<string>(failure),
			CancellationToken.None);

		(await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);

		logger.Entries.Should().Contain(e =>
			e.Level == LogLevel.Error && e.Exception == failure);
	}

	private sealed class CapturingLogger<T> : ILogger<T>
	{
		public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();
		public List<IReadOnlyDictionary<string, object>> Scopes { get; } = new();

		public IDisposable BeginScope<TState>(TState state) where TState : notnull
		{
			if (state is IEnumerable<KeyValuePair<string, object>> pairs)
			{
				Scopes.Add(pairs.ToDictionary(p => p.Key, p => p.Value));
			}

			return NullScope.Instance;
		}

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			Entries.Add((logLevel, formatter(state, exception), exception));
		}

		public string AllRenderedText() => string.Join("\n", Entries.Select(e => e.Message));

		private sealed class NullScope : IDisposable
		{
			public static readonly NullScope Instance = new();
			public void Dispose() { }
		}
	}
}
