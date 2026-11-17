using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Modules.Tenancy.Infrastructure;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Api.Middlewares;

public sealed class TenantResolutionMiddleware(
    RequestDelegate next,
    ILogger<TenantResolutionMiddleware> logger)
{
    private static readonly string[] TenantAgnosticPrefixes =
    [
        "/health",
        "/scalar",
        "/openapi",
        "/api/auth",
        "/api/account",
        "/api/invitations",
        "/api/platform"
    ];

    private static readonly string[] TenantAgnosticPaths =
    [
        "/api/plans",
        "/api/tenants/register",
        "/api/tenants/mine"
    ];

    public async Task InvokeAsync(
        HttpContext context,
        ITenancyResolver resolver,
        ITenantContext tenantContext,
        IActorContext actorContext,
        ICurrentUser currentUser,
        IHostEnvironment environment)
    {
        if (IsTenantAgnostic(context.Request.Path))
        {
            await next(context);
            return;
        }

        var isWebhook = context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                        && context.Request.Path.StartsWithSegments("/api/webhooks");
        var slug = ResolveHostSlug(context.Request.Host.Host)
                   ?? ResolveDevelopmentSlug(context.Request, environment);
        var userId = currentUser.IsAuthenticated ? currentUser.UserId : null;
        var allowInactiveSubscription = AllowsInactiveSubscription(context.Request);

        var result = await resolver.ResolveAsync(
            slug,
            userId,
            isWebhook,
            currentUser.IsPlatformAdmin,
            allowInactiveSubscription,
            context.RequestAborted);

        if (result.IsFailure)
        {
            await result.Error!.ToProblemResult().ExecuteAsync(context);
            return;
        }

        tenantContext.Set(result.Value);
        actorContext.Set(result.Value?.TenantId, result.Value?.Role);

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TenantId"] = result.Value?.TenantId,
            ["TenantSlug"] = result.Value?.TenantSlug,
            ["UserId"] = result.Value?.UserId
        });

        await next(context);
    }

    private static bool IsTenantAgnostic(PathString path)
    {
        if (TenantAgnosticPrefixes.Any(prefix => path.StartsWithSegments(prefix)))
        {
            return true;
        }

        return TenantAgnosticPaths.Any(candidate => path.Equals(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static bool AllowsInactiveSubscription(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method))
        {
            return true;
        }

        return request.Path.StartsWithSegments("/api/tenants/current/subscription")
               || request.Path.StartsWithSegments("/api/support");
    }

    private static string? ResolveHostSlug(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var normalized = host.ToLowerInvariant();
        if (normalized == "localhost" || normalized == "127.0.0.1" || normalized == "::1")
        {
            return null;
        }

        var labels = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < 2)
        {
            return null;
        }

        var candidate = labels[0];
        return Tenant.IsValidSlug(candidate) ? candidate : null;
    }

    private static string? ResolveDevelopmentSlug(HttpRequest request, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
        {
            return null;
        }

        var header = request.Headers["X-Tenant-Slug"].ToString().Trim();
        return Tenant.IsValidSlug(header) ? Tenant.NormalizeSlug(header) : null;
    }
}

public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder app)
        => app.UseMiddleware<TenantResolutionMiddleware>();
}
