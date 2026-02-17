using System;
using System.Security.Claims;

namespace CardGames.Poker.Web.Components.Shared;

internal static class UserProfileDisplayHelpers
{
    private static readonly string[] ApiBaseUrlEnvironmentKeys =
    [
        "ApiBaseUrl",
        "Services__Api__Https__0",
        "Services__api__https__0",
        "Services__Api__Http__0",
        "Services__api__http__0"
    ];

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

    public static string BuildAvatarApiPath(string userId)
    {
        var relativePath = $"/api/v1/profile/avatar/{Uri.EscapeDataString(userId)}";
        var apiBaseUrl = ResolveApiBaseUrl();
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            return relativePath;
        }

        var normalizedBaseUrl = apiBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? apiBaseUrl
            : apiBaseUrl + "/";

        return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), relativePath.TrimStart('/')).ToString();
    }

    public static string? BuildPreferredAvatarUrl(string? userId, string? avatarUrl)
    {
        var normalizedAvatarUrl = NormalizeAvatarUrl(avatarUrl);
        if (string.IsNullOrWhiteSpace(normalizedAvatarUrl))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return normalizedAvatarUrl.StartsWith("/", StringComparison.Ordinal) ? normalizedAvatarUrl : null;
        }

        if (normalizedAvatarUrl.StartsWith("/api/v1/profile/avatar/", StringComparison.OrdinalIgnoreCase))
        {
            var apiBaseUrl = ResolveApiBaseUrl();
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                return normalizedAvatarUrl;
            }

            var normalizedBaseUrl = apiBaseUrl.EndsWith("/", StringComparison.Ordinal)
                ? apiBaseUrl
                : apiBaseUrl + "/";

            return new Uri(new Uri(normalizedBaseUrl, UriKind.Absolute), normalizedAvatarUrl.TrimStart('/')).ToString();
        }

        return BuildAvatarApiPath(userId);
    }

    private static string? ResolveApiBaseUrl()
    {
        foreach (var key in ApiBaseUrlEnvironmentKeys)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                continue;
            }

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return uri.AbsoluteUri;
            }
        }

        return null;
    }
}