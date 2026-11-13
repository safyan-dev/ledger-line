namespace PaymentReconciliation.Modules.Identity.Application;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ledgerline";

    public string Audience { get; set; } = "ledgerline-api";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
