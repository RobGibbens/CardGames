using System;
using System.Security.Claims;

namespace CardGames.Poker.Web.Components.Shared;

internal static class UserProfileDisplayHelpers
{
    public static string? GetUserId(ClaimsPrincipal principal)
    {
        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
    }

    public static string GetUserInitial(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        return name[0].ToString().ToUpperInvariant();
    }

    public static string? GetFirstClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirstValue(claimType);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    public static string? NormalizeAvatarUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        url = url.Trim();

        if (url.StartsWith("/", StringComparison.Ordinal))
        {
            return url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return uri.ToString();
        }

        if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && (uri.IsLoopback || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)))
        {
            return uri.ToString();
        }

        return null;
    }

    public static string? PreferFirstName(string? firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName))
        {
            return null;
        }

        return firstName.Trim();
    }
}