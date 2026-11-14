using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Identity.Application;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Identity.Infrastructure;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<Microsoft.AspNetCore.Identity.IdentityOptions>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        });

        services.Configure<Microsoft.AspNetCore.Identity.DataProtectionTokenProviderOptions>(
            options => options.TokenLifespan = TimeSpan.FromHours(1));

        services.AddJwtAuthentication(configuration);
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<ILoginTenantResolver, LoginTenantResolver>();
        services.AddScoped<AuthService>();
        services.AddScoped<PasswordResetService>();
        services.AddScoped<IIdentityFacade, IdentityFacade>();
        services.AddScoped<ICurrentUser, JwtCurrentUser>();
        services.AddScoped<IActorContext, ActorContext>();
        services.AddScoped<IEmailSender, LoggingEmailSender>();

        return services;
    }
}
