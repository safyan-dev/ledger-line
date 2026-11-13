using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Modules.Identity.Infrastructure;

public sealed class JwtCurrentUser(
    IHttpContextAccessor httpContextAccessor,
    IActorContext actorContext) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public AuditActor Actor
    {
        get
        {
            if (UserId is not { } userId)
            {
                return AuditActor.System;
            }

            return new AuditActor(
                userId,
                Principal?.FindFirst("display_name")?.Value
                ?? Principal?.FindFirst(ClaimTypes.Name)?.Value
                ?? Principal?.FindFirst(ClaimTypes.Email)?.Value
                ?? "User",
                actorContext.Role ?? "User");
        }
    }

    public Guid? UserId
    {
        get
        {
            var value = Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                        ?? Principal?.FindFirst("sub")?.Value;
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Email => Principal?.FindFirst(ClaimTypes.Email)?.Value
                            ?? Principal?.FindFirst("email")?.Value;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool IsPlatformAdmin
        => string.Equals(Principal?.FindFirst("platform_admin")?.Value, "true", StringComparison.OrdinalIgnoreCase);
}
