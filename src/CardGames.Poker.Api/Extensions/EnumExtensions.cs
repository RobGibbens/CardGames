using System.ComponentModel;
using System.Reflection;

namespace CardGames.Poker.Api.Extensions;

public static class EnumExtensions
{
	extension(Enum value)
	{
		public string GetDescriptionOrName()
		{
			var memberName = value.ToString();
			var field = value.GetType().GetField(memberName, BindingFlags.Public | BindingFlags.Static);
			var description = field?.GetCustomAttribute<DescriptionAttribute>(inherit: false)?.Description;

			return string.IsNullOrWhiteSpace(description) ? memberName : description;
		}
	}
}
