using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentReconciliation.Modules.Identity.Domain;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Identity.Application;

public sealed class PasswordResetService(
    UserManager<ApplicationUser> userManager,
    AuthService authService,
    IHostEnvironment environment,
    ILogger<PasswordResetService> logger)
{
    public async Task<Result<ForgotPasswordResponse>> ForgotAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Success(new ForgotPasswordResponse(null));
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        logger.LogInformation("Password reset requested for user {UserId}.", user.Id);

        return Result.Success(new ForgotPasswordResponse(environment.IsDevelopment() ? token : null));
    }

    public async Task<Result> ResetAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure(Error.Validation(
                "auth.invalid_reset_token",
                "The reset token is invalid or has expired."));
        }

        var resetResult = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            var failures = resetResult.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());
            return Result.Failure(Error.Validation(
                "auth.identity_validation",
                "The password could not be reset.",
                failures));
        }

        await authService.RevokeAllAsync(user.Id, cancellationToken);
        return Result.Success();
    }
}
