using CardGames.Poker.Shared.Contracts.Auth;

namespace CardGames.Poker.Api.Features.Auth;

public static class AuthModule
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .AllowAnonymous();

        group.MapPost("/login", LoginAsync)
            .WithName("Login")
            .AllowAnonymous();

        group.MapGet("/me", GetCurrentUserAsync)
            .WithName("GetCurrentUser")
            .RequireAuthorization();

        group.MapGet("/providers", GetAuthProvidersAsync)
            .WithName("GetAuthProviders")
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new AuthResponse(false, Error: "Email and password are required"));
        }

        if (!IsValidEmail(request.Email))
        {
            return Results.BadRequest(new AuthResponse(false, Error: "Invalid email format"));
        }

        if (request.Password.Length < 8)
        {
            return Results.BadRequest(new AuthResponse(false, Error: "Password must be at least 8 characters"));
        }

        if (await userRepository.EmailExistsAsync(request.Email))
        {
            return Results.Conflict(new AuthResponse(false, Error: "An account with this email already exists"));
        }

        var passwordHash = passwordHasher.HashPassword(request.Password);
        var user = await userRepository.CreateAsync(request.Email, passwordHash, request.DisplayName);

        var token = tokenService.GenerateToken(user);
        var expiresAt = tokenService.GetExpiration();

        return Results.Ok(new AuthResponse(
            Success: true,
            Token: token,
            Email: user.Email,
            DisplayName: user.DisplayName,
            ExpiresAt: expiresAt
        ));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new AuthResponse(false, Error: "Email and password are required"));
        }

        var user = await userRepository.GetByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            return Results.Unauthorized();
        }

        var token = tokenService.GenerateToken(user);
        var expiresAt = tokenService.GetExpiration();

        return Results.Ok(new AuthResponse(
            Success: true,
            Token: token,
            Email: user.Email,
            DisplayName: user.DisplayName,
            ExpiresAt: expiresAt
        ));
    }

    private static IResult GetCurrentUserAsync(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        var email = user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value;
        var displayName = user.FindFirst("display_name")?.Value;
        var authProvider = user.FindFirst("auth_provider")?.Value;

        return Results.Ok(new UserInfo(
            Id: userId ?? string.Empty,
            Email: email ?? string.Empty,
            DisplayName: displayName,
            IsAuthenticated: true,
            AuthProvider: authProvider
        ));
    }

    private static IResult GetAuthProvidersAsync()
    {
        var providers = new[]
        {
            new { Name = "local", DisplayName = "Email/Password", Enabled = true },
            new { Name = "google", DisplayName = "Google", Enabled = false }
        };

        return Results.Ok(providers);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
