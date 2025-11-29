using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CardGames.Poker.Api.Features.Auth;

public interface IJwtSecretProvider
{
    string GetSecret();
}

internal class JwtSecretProvider : IJwtSecretProvider
{
    private readonly string _secret;

    public JwtSecretProvider(string? configuredSecret)
    {
        _secret = GetOrCreateSecret(configuredSecret);
    }

    public string GetSecret() => _secret;

    private static string GetOrCreateSecret(string? configuredSecret)
    {
        if (!string.IsNullOrEmpty(configuredSecret) && configuredSecret.Length >= 32)
        {
            return configuredSecret;
        }

        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }
}

public static class AuthServiceExtensions
{
    public static IServiceCollection AddAuthServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authSettings = configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>()
            ?? new AuthSettings();

        var secretProvider = new JwtSecretProvider(authSettings.JwtSecret);
        services.AddSingleton<IJwtSecretProvider>(secretProvider);

        services.Configure<AuthSettings>(options =>
        {
            configuration.GetSection(AuthSettings.SectionName).Bind(options);
            options.JwtSecret = secretProvider.GetSecret();
        });

        services.AddSingleton<IUserRepository, InMemoryUserRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = authSettings.JwtIssuer,
                ValidAudience = authSettings.JwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretProvider.GetSecret())),
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();

        return services;
    }
}
