using System.Globalization;

namespace CardGames.Poker.Web.Utilities;

public sealed record BrowserTimeZoneOption(string Id, string DisplayName);

public sealed record BrowserTimeZoneSetup(string? BrowserTimeZoneId, IReadOnlyList<BrowserTimeZoneOption> TimeZones);

public static class LeagueTimeZoneHelper
{
	public const string DefaultTimeText = "19:00";

	public static bool TryConvertLocalToUtc(DateTime? date, string? timeText, string? timeZoneId, out DateTimeOffset utcValue, out string? errorMessage)
	{
		utcValue = default;
		errorMessage = null;

		if (!date.HasValue)
		{
			errorMessage = "Scheduled date is required.";
			return false;
		}

		if (!TryParseTimeText(timeText, out var localTime))
		{
			errorMessage = "Scheduled time is required.";
			return false;
		}

		if (string.IsNullOrWhiteSpace(timeZoneId))
		{
			errorMessage = "Time zone is required.";
			return false;
		}

		if (!TryResolveTimeZone(timeZoneId, out var timeZone))
		{
			errorMessage = "Selected time zone is not supported.";
			return false;
		}

		var localDateTime = DateTime.SpecifyKind(date.Value.Date.Add(localTime.ToTimeSpan()), DateTimeKind.Unspecified);

		if (timeZone.IsInvalidTime(localDateTime))
		{
			errorMessage = "The selected date and time is invalid in that time zone.";
			return false;
		}

		if (timeZone.IsAmbiguousTime(localDateTime))
		{
			var offset = timeZone.GetAmbiguousTimeOffsets(localDateTime)
				.OrderByDescending(value => value)
				.First();
			utcValue = new DateTimeOffset(localDateTime, offset).ToUniversalTime();
			return true;
		}

		utcValue = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone), TimeSpan.Zero);
		return true;
	}

	public static DateTime? ConvertUtcToLocalDate(DateTimeOffset? utcValue, string? timeZoneId)
	{
		if (!utcValue.HasValue)
		{
			return null;
		}

		var localValue = ConvertUtcToLocal(utcValue.Value, timeZoneId);
		return localValue?.Date;
	}

	public static string ConvertUtcToTimeText(DateTimeOffset? utcValue, string? timeZoneId)
	{
		if (!utcValue.HasValue)
		{
			return DefaultTimeText;
		}

		var localValue = ConvertUtcToLocal(utcValue.Value, timeZoneId);
		return localValue?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? DefaultTimeText;
	}

	public static string FormatDateTime(DateTimeOffset? utcValue, string? timeZoneId)
	{
		if (!utcValue.HasValue)
		{
			return "-";
		}

		var localValue = ConvertUtcToLocal(utcValue.Value, timeZoneId);
		return localValue?.ToString("g", CultureInfo.CurrentCulture) ?? utcValue.Value.ToString("g", CultureInfo.CurrentCulture);
	}

	public static DateTime GetNextLocalEventDate(string? timeZoneId)
	{
		var localNow = ConvertUtcToLocal(DateTimeOffset.UtcNow, timeZoneId) ?? DateTimeOffset.UtcNow;
		return localNow.Date.AddDays(1);
	}

	private static DateTimeOffset? ConvertUtcToLocal(DateTimeOffset utcValue, string? timeZoneId)
	{
		if (!TryResolveTimeZone(timeZoneId, out var timeZone))
		{
			return null;
		}

		return TimeZoneInfo.ConvertTime(utcValue, timeZone);
	}

	private static bool TryParseTimeText(string? timeText, out TimeOnly localTime)
	{
		return TimeOnly.TryParseExact(timeText, ["HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out localTime)
			|| TimeOnly.TryParse(timeText, CultureInfo.CurrentCulture, out localTime);
	}

	private static bool TryResolveTimeZone(string? timeZoneId, out TimeZoneInfo timeZone)
	{
		if (string.IsNullOrWhiteSpace(timeZoneId))
		{
			timeZone = TimeZoneInfo.Utc;
			return false;
		}

		if (TryFindSystemTimeZone(timeZoneId, out timeZone))
		{
			return true;
		}

		if (TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZoneId, out var windowsId)
			&& TryFindSystemTimeZone(windowsId, out timeZone))
		{
			return true;
		}

		if (TimeZoneInfo.TryConvertWindowsIdToIanaId(timeZoneId, out var ianaId)
			&& TryFindSystemTimeZone(ianaId, out timeZone))
		{
			return true;
		}

		timeZone = TimeZoneInfo.Utc;
		return false;
	}

	private static bool TryFindSystemTimeZone(string timeZoneId, out TimeZoneInfo timeZone)
	{
		try
		{
			timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
			return true;
		}
		catch (ArgumentException)
		{
			timeZone = TimeZoneInfo.Utc;
			return false;
		}
		catch (TimeZoneNotFoundException)
		{
			timeZone = TimeZoneInfo.Utc;
			return false;
		}
		catch (InvalidTimeZoneException)
		{
			timeZone = TimeZoneInfo.Utc;
			return false;
		}
	}
}