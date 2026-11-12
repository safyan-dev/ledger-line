using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Auditing.Application;
using PaymentReconciliation.Modules.Auditing.Contracts;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Auditing.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit-logs").WithTags("Audit");

        group.MapGet("/", async (
            int? page,
            int? pageSize,
            string? module,
            string? action,
            string? entityType,
            Guid? entityId,
            string? search,
            IDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            var result = await dispatcher.SendAsync(
                new GetAuditLogsQuery(page ?? 1, pageSize ?? 25, module, action, entityType, entityId, search),
                cancellationToken);

            return result.ToHttpResult();
        })
        .WithName("GetAuditLogs")
        .Produces<PagedResult<AuditLogDto>>();

        return app;
    }
}
