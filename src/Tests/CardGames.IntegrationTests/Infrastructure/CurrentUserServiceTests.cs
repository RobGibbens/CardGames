using CardGames.Poker.Api.Infrastructure;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace CardGames.IntegrationTests.Infrastructure;

/// <summary>
/// Integration tests for <see cref="CurrentUserService"/>.
/// Tests user identity extraction from various claim types.
/// </summary>
public class CurrentUserServiceTests
{
    [Fact]
    public void UserId_FromNameIdentifierClaim_ReturnsValue()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "user-123") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var userId = service.UserId;

        // Assert
        userId.Should().Be("user-123");
    }

    [Fact]
    public void UserId_FromOidClaim_ReturnsValue()
    {
        // Arrange
        var claims = new[] { new Claim("oid", "oid-456") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var userId = service.UserId;

        // Assert
        userId.Should().Be("oid-456");
    }

    [Fact]
    public void UserId_FromSubClaim_ReturnsValue()
    {
        // Arrange
        var claims = new[] { new Claim("sub", "sub-789") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var userId = service.UserId;

        // Assert
        userId.Should().Be("sub-789");
    }

    [Fact]
    public void UserId_NoClaims_ReturnsNull()
    {
        // Arrange
        var identity = new ClaimsIdentity();
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var userId = service.UserId;

        // Assert
        userId.Should().BeNull();
    }

    [Fact]
    public void UserName_FromEmailClaim_ReturnsValue()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Email, "test@example.com") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var userName = service.UserName;

        // Assert
        userName.Should().Be("test@example.com");
    }

    [Fact]
    public void UserName_FromPreferredUsernameClaim_ReturnsValue()
    {
        // Arrange
        var claims = new[] { new Claim("preferred_username", "preferred_user") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var userName = service.UserName;

        // Assert
        userName.Should().Be("preferred_user");
    }

    [Fact]
    public void UserName_FromIdentityName_ReturnsValue()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Name, "identity_name") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var userName = service.UserName;

        // Assert
        userName.Should().Be("identity_name");
    }

    [Fact]
    public void UserEmail_FromEmailClaim_ReturnsValue()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Email, "email@test.com") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var email = service.UserEmail;

        // Assert
        email.Should().Be("email@test.com");
    }

    [Fact]
    public void UserEmail_FromLowercaseEmailClaim_ReturnsValue()
    {
        // Arrange
        var claims = new[] { new Claim("email", "lowercase@test.com") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var email = service.UserEmail;

        // Assert
        email.Should().Be("lowercase@test.com");
    }

    [Fact]
    public void UserEmail_NoClaim_ReturnsNull()
    {
        // Arrange
        var claims = new[] { new Claim(ClaimTypes.Name, "somename") };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var email = service.UserEmail;

        // Assert
        email.Should().BeNull();
    }

    [Fact]
    public void NoHttpContext_AllPropertiesReturnNull()
    {
        // Arrange
        var accessor = new TestHttpContextAccessor(null);
        var service = new CurrentUserService(accessor);

        // Assert
        service.UserId.Should().BeNull();
        service.UserName.Should().BeNull();
        service.UserEmail.Should().BeNull();
    }

    [Fact]
    public void ClaimPriority_EmailPreferredOverName()
    {
        // Arrange - Both email and name claims present
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, "email@test.com"),
            new Claim(ClaimTypes.Name, "Name User")
        };
        var identity = new ClaimsIdentity(claims, "test");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new TestHttpContextAccessor(httpContext);
        var service = new CurrentUserService(accessor);

        // Act
        var userName = service.UserName;

        // Assert - Email should take priority
        userName.Should().Be("email@test.com");
    }

    private class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }

        public TestHttpContextAccessor(HttpContext? context)
        {
            HttpContext = context;
        }
    }
}
