using System;
using System.ComponentModel.DataAnnotations;

namespace CardGames.Poker.Web.Infrastructure.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class OptionalUrlAttribute : ValidationAttribute
{
	public OptionalUrlAttribute()
	{
		ErrorMessage ??= "The {0} field is not a valid fully-qualified http, https, or ftp URL.";
	}

	public override bool IsValid(object? value)
	{
		if (value is null)
		{
			return true;
		}

		if (value is not string url)
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(url))
		{
			return true;
		}

		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
		{
			return false;
		}

		return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
			|| uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
			|| uri.Scheme.Equals(Uri.UriSchemeFtp, StringComparison.OrdinalIgnoreCase);
	}
}
