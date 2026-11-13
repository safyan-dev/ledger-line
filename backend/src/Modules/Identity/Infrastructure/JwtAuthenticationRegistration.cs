using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PaymentReconciliation.Modules.Identity.Application;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Identity.Infrastructure;

public static class JwtAuthenticationRegistration
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.SigningKey) && options.SigningKey.Length >= 32,
                "Jwt:SigningKey must be at least 32 characters.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((jwtBearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                if (string.IsNullOrWhiteSpace(jwt.SigningKey) || jwt.SigningKey.Length < 32)
                {
                    throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");
                }

                jwtBearer.MapInboundClaims = false;
                jwtBearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "display_name"
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AuthenticatedUser, policy =>
                policy.RequireAuthenticatedUser());
            options.AddPolicy(AuthorizationPolicies.PlatformAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireAssertion(context =>
                    string.Equals(
                        context.User.FindFirst("platform_admin")?.Value,
                        "true",
                        StringComparison.OrdinalIgnoreCase)));
        });

        return services;
    }
}
