using System;
using System.ComponentModel.DataAnnotations;

namespace CardGames.Poker.Web.Infrastructure.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class OptionalPhoneAttribute : ValidationAttribute
{
	private static readonly PhoneAttribute PhoneValidator = new();

	public OptionalPhoneAttribute()
	{
		ErrorMessage ??= "The {0} field is not a valid phone number.";
	}

	public override bool IsValid(object? value)
	{
		if (value is null)
		{
			return true;
		}

		if (value is not string phoneNumber)
		{
			return false;
		}

		if (string.IsNullOrWhiteSpace(phoneNumber))
		{
			return true;
		}

		return PhoneValidator.IsValid(phoneNumber);
	}
}