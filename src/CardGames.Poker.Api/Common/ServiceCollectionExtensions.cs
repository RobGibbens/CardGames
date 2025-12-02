using FluentValidation;
using MediatR;
using CardGames.Poker.Api.Common.Behaviors;

namespace CardGames.Poker.Api.Common;

/// <summary>
/// Extension methods for configuring MediatR and vertical slice infrastructure.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds MediatR, FluentValidation, and pipeline behaviors to the service collection.
    /// </summary>
    public static IServiceCollection AddMediatrWithBehaviors(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(ServiceCollectionExtensions).Assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(typeof(ServiceCollectionExtensions).Assembly);

        return services;
    }
}
