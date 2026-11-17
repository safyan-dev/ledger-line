using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Auditing.Application;
using PaymentReconciliation.Modules.Auditing.Contracts;
using PaymentReconciliation.Modules.Auditing.Domain;
using PaymentReconciliation.Modules.Auditing.Infrastructure;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Auditing;

public static class AuditingModule
{
    public static IServiceCollection AddAuditingModule(this IServiceCollection services)
    {
        services.AddCqrsHandlers(typeof(AuditLog).Assembly);
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditReporting, AuditReporting>();
        services.AddScoped<IPlatformAuditLogWriter, PlatformAuditLogWriter>();
        return services;
    }
}
