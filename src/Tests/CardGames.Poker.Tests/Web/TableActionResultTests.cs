using System.Net;
using CardGames.Poker.Api.Contracts;
using CardGames.Poker.Web.Services;
using CardGames.Poker.Web.Services.TableActions;
using FluentAssertions;
using NSubstitute;
using Refit;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TableActionResultTests
{
    [Fact]
    public void From_SuccessfulRouterResponse_IsSuccess()
    {
        var router = RouterResponse<Unit>.Success(new Unit());

        var result = TableActionResult.From(router);

        result.IsSuccess.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void From_FailedRouterResponse_NormalizesJsonError()
    {
        var router = RouterResponse<Unit>.Failure("{\"message\":\"Not enough chips\"}", HttpStatusCode.BadRequest);

        var result = TableActionResult.From(router);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Not enough chips");
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void From_SuccessfulApiResponse_PreservesContent()
    {
        var response = Substitute.For<IApiResponse<string>>();
        response.IsSuccessStatusCode.Returns(true);
        response.StatusCode.Returns(HttpStatusCode.OK);
        response.Content.Returns("payload");

        var result = TableActionResult<string>.From(response);

        result.IsSuccess.Should().BeTrue();
        result.Content.Should().Be("payload");
    }

    [Fact]
    public void From_FailedApiResponse_NoContent()
    {
        var response = Substitute.For<IApiResponse<string>>();
        response.IsSuccessStatusCode.Returns(false);
        response.StatusCode.Returns(HttpStatusCode.Conflict);
        response.Error.Returns((ApiException?)null);

        var result = TableActionResult<string>.From(response);

        result.IsSuccess.Should().BeFalse();
        result.Content.Should().BeNull();
        result.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
