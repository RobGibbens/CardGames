using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CardGames.Poker.Identity;

/// <summary>
///     Short-lived HMAC token that proves a caller identity between the Blazor host and the API.
///     Format: base64url(JSON payload) + "." + base64url(HMAC-SHA256(payload bytes)).
/// </summary>
public static class InternalUserToken
{
	public const int MinimumKeyLength = 32;
	public static readonly TimeSpan MaximumLifetime = TimeSpan.FromMinutes(15);
	public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public static string Issue(string signingKey, string subject, string? email, string? name, DateTimeOffset expiresAt)
	{
		var key = Encoding.UTF8.GetBytes(RequireKey(signingKey));
		var payload = new InternalUserTokenPayload(subject, email, name, expiresAt.ToUnixTimeSeconds());
		var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
		var signature = HMACSHA256.HashData(key, payloadBytes);
		return $"{Base64UrlEncode(payloadBytes)}.{Base64UrlEncode(signature)}";
	}

	public static bool TryValidate(string signingKey, string token, DateTimeOffset utcNow, out InternalUserTokenPayload? payload)
	{
		payload = null;
		if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < MinimumKeyLength)
			return false;

		var separator = token.IndexOf('.');
		if (separator <= 0 || separator != token.LastIndexOf('.'))
			return false;

		byte[] payloadBytes;
		byte[] providedSignature;
		try
		{
			payloadBytes = Base64UrlDecode(token[..separator]);
			providedSignature = Base64UrlDecode(token[(separator + 1)..]);
		}
		catch (FormatException)
		{
			return false;
		}

		var expectedSignature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingKey), payloadBytes);
		if (providedSignature.Length != expectedSignature.Length ||
		    !CryptographicOperations.FixedTimeEquals(providedSignature, expectedSignature))
			return false;

		InternalUserTokenPayload? parsed;
		try
		{
			parsed = JsonSerializer.Deserialize<InternalUserTokenPayload>(payloadBytes, JsonOptions);
		}
		catch (JsonException)
		{
			return false;
		}

		if (parsed is null || string.IsNullOrWhiteSpace(parsed.Sub))
			return false;

		var expiresAt = DateTimeOffset.FromUnixTimeSeconds(parsed.Exp);
		if (expiresAt + ClockSkew < utcNow)
			return false;

		if (expiresAt > utcNow + MaximumLifetime + ClockSkew)
			return false;

		payload = parsed;
		return true;
	}

	private static string RequireKey(string signingKey)
	{
		if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < MinimumKeyLength)
			throw new InvalidOperationException(
				$"InternalIdentity:SigningKey must be at least {MinimumKeyLength} characters.");

		return signingKey;
	}

	private static string Base64UrlEncode(byte[] bytes) =>
		Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

	private static byte[] Base64UrlDecode(string value)
	{
		var padded = value.Replace('-', '+').Replace('_', '/');
		switch (padded.Length % 4)
		{
			case 2:
				padded += "==";
				break;
			case 3:
				padded += "=";
				break;
		}

		return Convert.FromBase64String(padded);
	}
}

public sealed record InternalUserTokenPayload(string Sub, string? Email, string? Name, long Exp);
