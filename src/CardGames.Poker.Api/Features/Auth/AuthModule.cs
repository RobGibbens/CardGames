using CardGames.Poker.Shared.Contracts.Auth;
using Microsoft.Extensions.Options;

namespace CardGames.Poker.Api.Features.Auth;

public static class AuthModule
{
    private const long DefaultInitialChipBalance = 1000;

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

        group.MapPut("/profile", UpdateProfileAsync)
            .WithName("UpdateProfile")
            .RequireAuthorization();

        group.MapGet("/chips", GetChipBalanceAsync)
            .WithName("GetChipBalance")
            .RequireAuthorization();

        group.MapPost("/chips/adjust", AdjustChipBalanceAsync)
            .WithName("AdjustChipBalance")
            .RequireAuthorization();

        group.MapPut("/chips", SetChipBalanceAsync)
            .WithName("SetChipBalance")
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IOptions<AuthSettings> authSettings)
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

        var initialChipBalance = authSettings.Value.InitialChipBalance ?? DefaultInitialChipBalance;
        var passwordHash = passwordHasher.HashPassword(request.Password);
        var user = await userRepository.CreateAsync(request.Email, passwordHash, request.DisplayName, initialChipBalance);

        var token = tokenService.GenerateToken(user);
        var expiresAt = tokenService.GetExpiration();

        return Results.Ok(new AuthResponse(
            Success: true,
            Token: token,
            Email: user.Email,
            DisplayName: user.DisplayName,
            ExpiresAt: expiresAt,
            ChipBalance: user.ChipBalance,
            AvatarUrl: user.AvatarUrl
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
            ExpiresAt: expiresAt,
            ChipBalance: user.ChipBalance,
            AvatarUrl: user.AvatarUrl
        ));
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        IUserRepository userRepository)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var userId = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var userRecord = await userRepository.GetByIdAsync(userId);
        if (userRecord is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new UserInfo(
            Id: userRecord.Id,
            Email: userRecord.Email,
            DisplayName: userRecord.DisplayName,
            IsAuthenticated: true,
            AuthProvider: userRecord.AuthProvider,
            ChipBalance: userRecord.ChipBalance,
            AvatarUrl: userRecord.AvatarUrl
        ));
    }

    private static async Task<IResult> UpdateProfileAsync(
        UpdateProfileRequest request,
        HttpContext context,
        IUserRepository userRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var updatedUser = await userRepository.UpdateProfileAsync(userId, request.DisplayName, request.AvatarUrl);
        if (updatedUser is null)
        {
            return Results.NotFound(new ProfileResponse(false, Error: "User not found"));
        }

        return Results.Ok(new ProfileResponse(
            Success: true,
            Id: updatedUser.Id,
            Email: updatedUser.Email,
            DisplayName: updatedUser.DisplayName,
            AvatarUrl: updatedUser.AvatarUrl,
            ChipBalance: updatedUser.ChipBalance
        ));
    }

    private static async Task<IResult> GetChipBalanceAsync(
        HttpContext context,
        IUserRepository userRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return Results.NotFound(new ChipBalanceResponse(false, Error: "User not found"));
        }

        return Results.Ok(new ChipBalanceResponse(true, Balance: user.ChipBalance));
    }

    private static async Task<IResult> AdjustChipBalanceAsync(
        UpdateChipBalanceRequest request,
        HttpContext context,
        IUserRepository userRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        try
        {
            var updatedUser = await userRepository.AdjustChipBalanceAsync(userId, request.Amount);
            if (updatedUser is null)
            {
                return Results.NotFound(new ChipBalanceResponse(false, Error: "User not found"));
            }

            return Results.Ok(new ChipBalanceResponse(true, Balance: updatedUser.ChipBalance));
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ChipBalanceResponse(false, Error: ex.Message));
        }
    }

    private static async Task<IResult> SetChipBalanceAsync(
        SetChipBalanceRequest request,
        HttpContext context,
        IUserRepository userRepository)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
        {
            return Results.Unauthorized();
        }

        if (request.Balance < 0)
        {
            return Results.BadRequest(new ChipBalanceResponse(false, Error: "Chip balance cannot be negative"));
        }

        try
        {
            var updatedUser = await userRepository.UpdateChipBalanceAsync(userId, request.Balance);
            if (updatedUser is null)
            {
                return Results.NotFound(new ChipBalanceResponse(false, Error: "User not found"));
            }

            return Results.Ok(new ChipBalanceResponse(true, Balance: updatedUser.ChipBalance));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ChipBalanceResponse(false, Error: ex.Message));
        }
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

    private static string? GetUserId(HttpContext context)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        return user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
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
