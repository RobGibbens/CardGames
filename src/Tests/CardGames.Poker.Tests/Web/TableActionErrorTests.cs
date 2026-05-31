using CardGames.Poker.Web.Services.TableActions;
using FluentAssertions;
using Xunit;

namespace CardGames.Poker.Tests.Web;

public class TableActionErrorTests
{
    [Fact]
    public void FromBody_NullOrWhitespace_ReturnsNull()
    {
        TableActionError.FromBody(null).Should().BeNull();
        TableActionError.FromBody("").Should().BeNull();
        TableActionError.FromBody("   ").Should().BeNull();
    }

    [Fact]
    public void FromBody_PlainText_ReturnsAsIs()
    {
        TableActionError.FromBody("Something went wrong").Should().Be("Something went wrong");
    }

    [Fact]
    public void FromBody_JsonWithLowercaseMessage_ExtractsMessage()
    {
        TableActionError.FromBody("{\"message\":\"Not your turn\"}").Should().Be("Not your turn");
    }

    [Fact]
    public void FromBody_JsonWithUppercaseMessage_ExtractsMessage()
    {
        TableActionError.FromBody("{\"Message\":\"Invalid bet\"}").Should().Be("Invalid bet");
    }

    [Fact]
    public void FromBody_JsonWithoutMessageProperty_ReturnsRawBody()
    {
        const string body = "{\"code\":42}";
        TableActionError.FromBody(body).Should().Be(body);
    }

    [Fact]
    public void FromBody_MalformedJson_ReturnsRawBody()
    {
        const string body = "{not valid json";
        TableActionError.FromBody(body).Should().Be(body);
    }

    [Fact]
    public void FromException_Null_ReturnsFallback()
    {
        TableActionError.FromException(null).Should().Be("An unknown error occurred.");
        TableActionError.FromException(null, "boom").Should().Be("boom");
    }
}
