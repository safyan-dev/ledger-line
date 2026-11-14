using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Tenancy.Contracts;

public static class TenantPermissionFilter
{
    public static RouteHandlerBuilder RequireTenantPermission(
        this RouteHandlerBuilder builder,
        TenantPermission permission)
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var authorizer = context.HttpContext.RequestServices.GetRequiredService<ITenantAuthorizer>();
            var result = authorizer.Require(permission);

            return result.IsFailure
                ? result.Error!.ToProblemResult()
                : await next(context);
        });
}
