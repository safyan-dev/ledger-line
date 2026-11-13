using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using PaymentReconciliation.Modules.Identity.Application;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Web;

namespace PaymentReconciliation.Modules.Identity.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Authentication");

        group.MapPost("/register", async (
            RegisterRequest request,
            AuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RegisterAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("RegisterUser")
        .Produces<AuthResponse>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (
            LoginRequest request,
            AuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("Login")
        .Produces<AuthResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", async (
            RefreshRequest request,
            AuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RefreshAsync(request.RefreshToken, cancellationToken);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("RefreshToken")
        .Produces<AuthResponse>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (
            RefreshRequest request,
            AuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LogoutAsync(request.RefreshToken, cancellationToken);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("Logout")
        .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", async (AuthService authService, CancellationToken cancellationToken) =>
        {
            var result = await authService.GetCurrentUserAsync(cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("CurrentUser")
        .Produces<AuthUserDto>()
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/change-password", async (
            ChangePasswordRequest request,
            AuthService authService,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.ChangePasswordAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .RequireAuthorization(AuthorizationPolicies.AuthenticatedUser)
        .WithName("ChangePassword")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/forgot-password", async (
            ForgotPasswordRequest request,
            PasswordResetService passwordResetService,
            CancellationToken cancellationToken) =>
        {
            var result = await passwordResetService.ForgotAsync(request.Email, cancellationToken);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("ForgotPassword")
        .Produces<ForgotPasswordResponse>(StatusCodes.Status200OK);

        group.MapPost("/reset-password", async (
            ResetPasswordRequest request,
            PasswordResetService passwordResetService,
            CancellationToken cancellationToken) =>
        {
            var result = await passwordResetService.ResetAsync(request, cancellationToken);
            return result.ToHttpResult();
        })
        .AllowAnonymous()
        .WithName("ResetPassword")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
