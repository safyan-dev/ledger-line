using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PaymentReconciliation.Modules.Identity.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Events;

namespace PaymentReconciliation.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Database is not configured. Run backend/tools/setup-dev-secrets.sh (or .ps1) or set ConnectionStrings__Database.");
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
        {
            Name = "ledgerline"
        };
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(dataSource)
            .UseSnakeCaseNamingConvention());

        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<ITenantBoundaryBypass, TenantBoundaryBypass>();
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<DemoDataSeeder>();
        services.AddHostedService<SnakeCaseModelValidator>();

        return services;
    }
}
