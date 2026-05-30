using System.Security.Claims;

namespace CardGames.Poker.Web.Infrastructure;

/// <summary>
/// DelegatingHandler that attaches the signed internal-user token for API calls.
/// </summary>
public class AuthenticationStateHandler(
    CircuitServicesAccessor circuitServicesAccessor,
    IHttpContextAccessor httpContextAccessor,
    InternalApiUserTokenFactory internalApiUserTokenFactory) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var user = circuitServicesAccessor.User ?? httpContextAccessor.HttpContext?.User;

        if (user is not null)
        {
            var token = internalApiUserTokenFactory.CreateToken(user);
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.TryAddWithoutValidation(
                    InternalApiAuthOptions.InternalTokenHeaderName,
                    token);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
