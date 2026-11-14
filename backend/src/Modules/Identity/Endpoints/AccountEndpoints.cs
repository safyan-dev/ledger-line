using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Identity.Application;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Identity.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account").WithTags("Account");

        group.MapGet("/profile", async (AuthService authService, CancellationToken cancellationToken) =>
        {
            var result = await authService.GetCurrentUserAsync(cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("AccountProfile")
        .Produces<AuthUserDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/profile", async (
            UpdateProfileRequest request,
            AuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.UpdateProfileAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("UpdateAccountProfile")
        .Produces<AuthUserDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/change-email", async (
            ChangeEmailRequest request,
            AuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RequestEmailChangeAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("RequestEmailChange")
        .Produces<ChangeEmailResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/change-email/confirm", async (
            ConfirmEmailChangeRequest request,
            AuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.ConfirmEmailChangeAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("ConfirmEmailChange")
        .Produces<AuthUserDto>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
