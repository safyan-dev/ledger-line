using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PaymentReconciliation.Modules.Identity.Application;
using PaymentReconciliation.Modules.Identity.Domain;
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

                jwtBearer.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userIdClaim = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        var securityStamp = context.Principal?.FindFirst("security_stamp")?.Value;
                        if (!Guid.TryParse(userIdClaim, out var userId)
                            || string.IsNullOrWhiteSpace(securityStamp))
                        {
                            context.Fail("Token is missing required claims.");
                            return;
                        }

                        var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                        var user = await userManager.FindByIdAsync(userId.ToString());
                        if (user is null || user.Status != UserStatus.Active || !user.EmailConfirmed
                            || !string.Equals(
                                user.SecurityStamp,
                                securityStamp,
                                StringComparison.Ordinal))
                        {
                            context.Fail("Token is no longer valid.");
                        }
                    }
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
