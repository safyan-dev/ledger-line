using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Reporting.Application;
using PaymentReconciliation.SharedKernel.Cqrs;

namespace PaymentReconciliation.Modules.Reporting;

public static class ReportingModule
{
    public static IServiceCollection AddReportingModule(this IServiceCollection services)
    {
        services.AddCqrsHandlers(typeof(GetDashboardSummaryHandler).Assembly);
        return services;
    }
}
