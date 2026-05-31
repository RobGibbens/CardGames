#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardGames.Poker.Api.Infrastructure.PipelineBehaviors;
using CardGames.Poker.Api.Infrastructure.Telemetry;
using FluentAssertions;
using MediatR;
using Xunit;

namespace CardGames.Poker.Tests.Api.Infrastructure.PipelineBehaviors;

public class TracingPipelineBehaviorTests
{
	private sealed record SensitiveRequest(string Password, string Email) : IRequest<string>;

	[Fact]
	public async Task Handle_OnSuccess_StartsActivityWithOkStatus()
	{
		using var capture = new ActivityCapture();
		var behavior = new TracingPipelineBehavior<SensitiveRequest, string>();
		var request = new SensitiveRequest("super-secret-token", "player@example.com");

		var response = await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

		response.Should().Be("ok");

		var activity = capture.Activities.Should().ContainSingle().Subject;
		activity.DisplayName.Should().Be($"mediatr.{nameof(SensitiveRequest)}");
		activity.Status.Should().Be(ActivityStatusCode.Ok);
	}

	[Fact]
	public async Task Handle_OnFailure_SetsErrorStatusAndRethrows()
	{
		using var capture = new ActivityCapture();
		var behavior = new TracingPipelineBehavior<SensitiveRequest, string>();
		var request = new SensitiveRequest("pw", "e");
		var failure = new InvalidOperationException("boom");

		var act = async () => await behavior.Handle(
			request,
			_ => Task.FromException<string>(failure),
			CancellationToken.None);

		(await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);

		var activity = capture.Activities.Should().ContainSingle().Subject;
		activity.Status.Should().Be(ActivityStatusCode.Error);
	}

	[Fact]
	public async Task Handle_DoesNotRecordRequestPropertyValues()
	{
		using var capture = new ActivityCapture();
		var behavior = new TracingPipelineBehavior<SensitiveRequest, string>();
		var request = new SensitiveRequest("super-secret-token", "player@example.com");

		await behavior.Handle(request, _ => Task.FromResult("ok"), CancellationToken.None);

		var activity = capture.Activities.Should().ContainSingle().Subject;

		activity.DisplayName.Should().Be($"mediatr.{nameof(SensitiveRequest)}");
		activity.GetTagItem("mediatr.request_type").Should().Be(nameof(SensitiveRequest));

		var serialized = string.Join("\n", new[] { activity.DisplayName }
			.Concat(activity.Tags.Select(t => $"{t.Key}={t.Value}")));
		serialized.Should().NotContain("super-secret-token");
		serialized.Should().NotContain("player@example.com");
	}

	private sealed class ActivityCapture : IDisposable
	{
		private readonly ActivityListener _listener;

		public ActivityCapture()
		{
			_listener = new ActivityListener
			{
				ShouldListenTo = source => source.Name == PokerActivitySource.Name,
				Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
				ActivityStarted = activity => Activities.Add(activity),
			};

			ActivitySource.AddActivityListener(_listener);
		}

		public List<Activity> Activities { get; } = new();

		public void Dispose() => _listener.Dispose();
	}
}
