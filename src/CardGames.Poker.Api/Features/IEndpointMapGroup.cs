namespace CardGames.Poker.Api.Features;

/// <summary>
/// Marks a static class as an endpoint map group that can be discovered via assembly scanning.
/// The class must have a static MapEndpoints(IEndpointRouteBuilder) method.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class EndpointMapGroupAttribute : Attribute;

