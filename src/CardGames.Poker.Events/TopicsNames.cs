using System.Reflection;

namespace CardGames.Poker.Events;

public static partial class TopicNames
{
	public static IReadOnlyList<string> GetTopics(Type parentType) =>
		parentType
			.GetNestedTypes(BindingFlags.Public) // add NonPublic if you want internal/private too
			.Where(t => t.IsClass)
			.Select(t => t.Name)
			.ToArray();

	public static List<string?> GetTopicSubscriptions(Type nestedClassType)
	{
		return nestedClassType == null
			? throw new ArgumentNullException(nameof(nestedClassType))
			: nestedClassType
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(field => field.FieldType == typeof(string))
				.Select(field => field.GetValue(null)?.ToString())
				.Where(value => value != null)
				.ToList();
	}

	public static IReadOnlyList<string> GetStringValuesUnderChild(Type parentType, string childClassName)
	{
		var childType = parentType.GetNestedType(childClassName, BindingFlags.Public | BindingFlags.NonPublic);
		if (childType is null)
		{
			return [];
		}

		var results = new HashSet<string>(StringComparer.Ordinal);

		// Look at immediate nested types of the child (e.g., IngredientCreated/Deleted/Updated)
		foreach (var grandchild in childType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
		{
			// Find public static string fields (add NonPublic if needed)
			var fields = grandchild.GetFields(BindingFlags.Public | BindingFlags.Static);
			foreach (var f in fields.Where(f => f.FieldType == typeof(string)))
			{
				if (f.GetValue(null) is string s)
				{
					results.Add(s);
				}
			}
		}

		return results.ToArray();
	}

	// Convenience: process many child names (e.g., "Categories", "Favorites", "Ingredients")
	public static IReadOnlyList<string> GetStringValuesUnderChildren(Type parentType, IEnumerable<string> childClassNames)
	{
		var results = new HashSet<string>(StringComparer.Ordinal);
		foreach (var name in childClassNames)
		{
			foreach (var s in GetStringValuesUnderChild(parentType, name))
			{
				results.Add(s);
			}
		}
		return results.ToArray();
	}
}