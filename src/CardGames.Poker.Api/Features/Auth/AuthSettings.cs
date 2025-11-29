namespace CardGames.Poker.Api.Features.Auth;

public class AuthSettings
{
    public const string SectionName = "Authentication";
    
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "CardGames.Poker.Api";
    public string JwtAudience { get; set; } = "CardGames.Poker.Web";
    public int JwtExpirationMinutes { get; set; } = 60;
    
    public AzureB2CSettings? AzureB2C { get; set; }
}

public class AzureB2CSettings
{
    public string Instance { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string SignUpSignInPolicyId { get; set; } = string.Empty;
}
