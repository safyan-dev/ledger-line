using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Events;

namespace PaymentReconciliation.Persistence;

public static class PersistenceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database")
                               ?? "Host=localhost;Port=5432;Database=payment_reconciliation;Username=postgres;Password=postgres";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString)
        {
            Name = "ledgerline"
        };
        var dataSource = dataSourceBuilder.Build();

        services.AddSingleton(dataSource);
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(dataSource)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
        services.AddScoped<DemoDataSeeder>();
        services.AddHostedService<SnakeCaseModelValidator>();

        return services;
    }
}
