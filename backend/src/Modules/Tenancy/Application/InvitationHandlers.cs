using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Tenancy.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record GetTenantInvitationsQuery(
    string? Status,
    int Page = 1,
    int PageSize = 25) : IQuery<PagedResult<TenantInvitationDto>>;

public sealed class GetTenantInvitationsHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ITenantAuthorizer authorizer)
    : IQueryHandler<GetTenantInvitationsQuery, PagedResult<TenantInvitationDto>>
{
    public async Task<Result<PagedResult<TenantInvitationDto>>> Handle(
        GetTenantInvitationsQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<PagedResult<TenantInvitationDto>>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.InviteTeamMembers);
        if (authorization.IsFailure)
        {
            return Result.Failure<PagedResult<TenantInvitationDto>>(authorization.Error!);
        }

        InvitationStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<InvitationStatus>(query.Status.Trim(), ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Result.Failure<PagedResult<TenantInvitationDto>>(Error.Validation(
                    "invitation.invalid_status",
                    "Status must be Pending, Accepted, Expired or Revoked."));
            }

            statusFilter = parsed;
        }

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var now = DateTimeOffset.UtcNow;

        var invitations = dbContext.Set<TenantInvitation>()
            .AsNoTracking()
            .Where(invitation => invitation.TenantId == tenantId);

        if (statusFilter is { } status)
        {
            invitations = status == InvitationStatus.Expired
                ? invitations.Where(invitation =>
                    invitation.Status == InvitationStatus.Expired
                    || (invitation.Status == InvitationStatus.Pending && invitation.ExpiresAt <= now))
                : invitations.Where(invitation => invitation.Status == status);
        }

        var totalCount = await invitations.CountAsync(cancellationToken);
        var rows = await invitations
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(invitation => ToDto(invitation, now))
            .ToList();

        return Result.Success(PagedResult<TenantInvitationDto>.Create(items, page, pageSize, totalCount));
    }

    internal static TenantInvitationDto ToDto(TenantInvitation invitation, DateTimeOffset now)
        => new(
            invitation.Id,
            invitation.Email,
            invitation.Role.ToString(),
            invitation.EffectiveStatus(now).ToString(),
            invitation.ExpiresAt,
            invitation.CreatedAt,
            invitation.LastSentAt);
}

public sealed record CreateTenantInvitationCommand(string Email, string Role) : ICommand<IssuedTenantInvitationDto>;

public sealed class CreateTenantInvitationHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IIdentityFacade identity,
    ITenantAuthorizer authorizer,
    IEmailSender emailSender,
    IHostEnvironment environment)
    : ICommandHandler<CreateTenantInvitationCommand, IssuedTenantInvitationDto>
{
    public async Task<Result<IssuedTenantInvitationDto>> Handle(
        CreateTenantInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.InviteTeamMembers);
        if (authorization.IsFailure)
        {
            return Result.Failure<IssuedTenantInvitationDto>(authorization.Error!);
        }

        if (!TryNormalizeEmail(command.Email, out var email))
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.Validation(
                "invitation.invalid_email",
                "A valid email address is required."));
        }

        if (!TenantRoleParsing.TryParseRole(command.Role, out var role))
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.Validation(
                "tenant.invalid_role",
                "Role must be Owner, TenantAdmin or Finance."));
        }

        if (role == TenantRole.Owner)
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.Forbidden(
                "tenant.role_forbidden",
                "Owners cannot be invited. Assign the Owner role through an existing owner."));
        }

        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.NotFound(
                "tenant.not_found",
                "Tenant was not found."));
        }

        var existingUser = await identity.FindByEmailAsync(email, cancellationToken);
        if (existingUser.IsSuccess)
        {
            var isMember = await dbContext.Set<TenantUser>()
                .AnyAsync(
                    membership => membership.TenantId == tenantId && membership.UserId == existingUser.Value.Id,
                    cancellationToken);

            if (isMember)
            {
                return Result.Failure<IssuedTenantInvitationDto>(Error.Conflict(
                    "tenant.member_exists",
                    "This user is already a member of the tenant."));
            }
        }

        var now = DateTimeOffset.UtcNow;
        var pending = await dbContext.Set<TenantInvitation>()
            .FirstOrDefaultAsync(
                invitation => invitation.TenantId == tenantId
                              && invitation.Email == email
                              && invitation.Status == InvitationStatus.Pending,
                cancellationToken);

        if (pending is not null && !pending.IsExpired(now))
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.Conflict(
                "invitation.pending_exists",
                "A pending invitation already exists for this email. Resend it instead."));
        }

        var token = InvitationTokens.Create();
        var expiresAt = now.Add(TenantInvitation.DefaultLifetime);
        var actor = currentUser.Actor;

        if (pending is not null)
        {
            pending.Resend(InvitationTokens.Hash(token), expiresAt, now, actor);
            await SendInvitationEmailAsync(tenant, pending.Email, role, token, cancellationToken);

            return Result.Success(new IssuedTenantInvitationDto(
                GetTenantInvitationsHandler.ToDto(pending, now),
                TokenFor(environment, token)));
        }

        var invitation = TenantInvitation.Create(
            tenantId,
            email,
            role,
            InvitationTokens.Hash(token),
            expiresAt,
            currentUser.UserId ?? Guid.Empty,
            now,
            actor);

        dbContext.Set<TenantInvitation>().Add(invitation);
        await SendInvitationEmailAsync(tenant, invitation.Email, role, token, cancellationToken);

        return Result.Success(new IssuedTenantInvitationDto(
            GetTenantInvitationsHandler.ToDto(invitation, now),
            TokenFor(environment, token)));
    }

    internal static bool TryNormalizeEmail(string? value, out string email)
    {
        email = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.Length > 320 || !MailAddress.TryCreate(candidate, out var address))
        {
            return false;
        }

        if (!string.Equals(address.Address, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        email = TenantInvitation.NormalizeEmail(candidate);
        return true;
    }

    private static string? TokenFor(IHostEnvironment environment, string token)
        => environment.IsDevelopment() ? token : null;

    private Task SendInvitationEmailAsync(
        Tenant tenant,
        string email,
        TenantRole role,
        string token,
        CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            email,
            $"Invitation to {tenant.Name}",
            $"You have been invited to join '{tenant.Name}' as {role}. "
            + $"Accept the invitation with this token: {token}");

        return emailSender.SendAsync(message, cancellationToken);
    }
}

public sealed record ResendTenantInvitationCommand(Guid InvitationId) : ICommand<IssuedTenantInvitationDto>;

public sealed class ResendTenantInvitationHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    ITenantAuthorizer authorizer,
    IEmailSender emailSender,
    IHostEnvironment environment)
    : ICommandHandler<ResendTenantInvitationCommand, IssuedTenantInvitationDto>
{
    public async Task<Result<IssuedTenantInvitationDto>> Handle(
        ResendTenantInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.InviteTeamMembers);
        if (authorization.IsFailure)
        {
            return Result.Failure<IssuedTenantInvitationDto>(authorization.Error!);
        }

        var invitation = await dbContext.Set<TenantInvitation>()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.Id == command.InvitationId,
                cancellationToken);

        if (invitation is null)
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.NotFound(
                "invitation.not_found",
                "The invitation was not found."));
        }

        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<IssuedTenantInvitationDto>(Error.NotFound(
                "tenant.not_found",
                "Tenant was not found."));
        }

        var now = DateTimeOffset.UtcNow;
        var token = InvitationTokens.Create();
        invitation.Resend(
            InvitationTokens.Hash(token),
            now.Add(TenantInvitation.DefaultLifetime),
            now,
            currentUser.Actor);

        await SendInvitationEmailAsync(tenant, invitation.Email, invitation.Role, token, cancellationToken);

        return Result.Success(new IssuedTenantInvitationDto(
            GetTenantInvitationsHandler.ToDto(invitation, now),
            environment.IsDevelopment() ? token : null));
    }

    private Task SendInvitationEmailAsync(
        Tenant tenant,
        string email,
        TenantRole role,
        string token,
        CancellationToken cancellationToken)
    {
        var message = new EmailMessage(
            email,
            $"Invitation to {tenant.Name}",
            $"You have been invited to join '{tenant.Name}' as {role}. "
            + $"Accept the invitation with this token: {token}");

        return emailSender.SendAsync(message, cancellationToken);
    }
}

public sealed record RevokeTenantInvitationCommand(Guid InvitationId) : ICommand<TenantInvitationDto>;

public sealed class RevokeTenantInvitationHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    ITenantAuthorizer authorizer)
    : ICommandHandler<RevokeTenantInvitationCommand, TenantInvitationDto>
{
    public async Task<Result<TenantInvitationDto>> Handle(
        RevokeTenantInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TenantInvitationDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var authorization = authorizer.Require(TenantPermission.InviteTeamMembers);
        if (authorization.IsFailure)
        {
            return Result.Failure<TenantInvitationDto>(authorization.Error!);
        }

        var invitation = await dbContext.Set<TenantInvitation>()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.Id == command.InvitationId,
                cancellationToken);

        if (invitation is null)
        {
            return Result.Failure<TenantInvitationDto>(Error.NotFound(
                "invitation.not_found",
                "The invitation was not found."));
        }

        var now = DateTimeOffset.UtcNow;
        invitation.Revoke(now, currentUser.Actor);

        return Result.Success(GetTenantInvitationsHandler.ToDto(invitation, now));
    }
}

public sealed record AcceptTenantInvitationCommand(
    string Token,
    string? DisplayName,
    string? Password) : ICommand<AcceptTenantInvitationResponse>;

public sealed class AcceptTenantInvitationHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IIdentityFacade identity,
    IUsageLimitEnforcer usage)
    : ICommandHandler<AcceptTenantInvitationCommand, AcceptTenantInvitationResponse>
{
    public async Task<Result<AcceptTenantInvitationResponse>> Handle(
        AcceptTenantInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return Result.Failure<AcceptTenantInvitationResponse>(Error.Validation(
                "invitation.invalid_token",
                "The invitation token is invalid or has expired."));
        }

        var tokenHash = InvitationTokens.Hash(command.Token.Trim());
        var invitationId = await dbContext.Set<TenantInvitation>()
            .IgnoreQueryFilters()
            .Where(candidate => candidate.TokenHash == tokenHash)
            .Select(candidate => candidate.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (invitationId == Guid.Empty)
        {
            return Result.Failure<AcceptTenantInvitationResponse>(Error.Validation(
                "invitation.invalid_token",
                "The invitation token is invalid or has expired."));
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM tenancy.tenant_invitations WHERE id = {invitationId} FOR UPDATE",
            cancellationToken);

        var invitation = await dbContext.Set<TenantInvitation>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(candidate => candidate.Id == invitationId, cancellationToken);

        if (invitation is null || !string.Equals(invitation.TokenHash, tokenHash, StringComparison.Ordinal))
        {
            return Result.Failure<AcceptTenantInvitationResponse>(Error.Validation(
                "invitation.invalid_token",
                "The invitation token is invalid or has expired."));
        }

        var now = DateTimeOffset.UtcNow;
        switch (invitation.EffectiveStatus(now))
        {
            case InvitationStatus.Accepted:
                return Result.Failure<AcceptTenantInvitationResponse>(Error.Conflict(
                    "invitation.already_accepted",
                    "This invitation has already been accepted."));
            case InvitationStatus.Revoked:
                return Result.Failure<AcceptTenantInvitationResponse>(Error.Conflict(
                    "invitation.revoked",
                    "This invitation has been revoked."));
            case InvitationStatus.Expired:
                return Result.Failure<AcceptTenantInvitationResponse>(Error.Conflict(
                    "invitation.expired",
                    "This invitation has expired."));
        }

        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == invitation.TenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<AcceptTenantInvitationResponse>(Error.NotFound(
                "tenant.not_found",
                "Tenant was not found."));
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Result.Failure<AcceptTenantInvitationResponse>(Error.Forbidden(
                "tenant.inactive",
                "Tenant is not active."));
        }

        var userResult = await ResolveUserAsync(invitation, command, cancellationToken);
        if (userResult.IsFailure)
        {
            return Result.Failure<AcceptTenantInvitationResponse>(userResult.Error!);
        }

        var user = userResult.Value;
        tenantContext.Set(new TenantContextSnapshot(
            invitation.TenantId,
            tenant.Slug,
            user.UserId,
            null,
            currentUser.IsPlatformAdmin));

        var actor = new AuditActor(user.UserId, user.DisplayName, invitation.Role.ToString());
        var membership = await dbContext.Set<TenantUser>()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == invitation.TenantId && candidate.UserId == user.UserId,
                cancellationToken);

        if (membership is null)
        {
            var limit = await usage.EnsureWithinLimitAsync(UsageMetric.TeamMembers, cancellationToken);
            if (limit.IsFailure)
            {
                return Result.Failure<AcceptTenantInvitationResponse>(limit.Error!);
            }

            dbContext.Set<TenantUser>().Add(
                TenantUser.Add(invitation.TenantId, user.UserId, invitation.Role, actor));
        }

        invitation.Accept(user.UserId, now, actor);

        AuthSessionDto? session = null;
        if (user.IsNewUser)
        {
            var issued = await identity.IssueSessionAsync(user.UserId, cancellationToken);
            if (issued.IsFailure)
            {
                return Result.Failure<AcceptTenantInvitationResponse>(issued.Error!);
            }

            session = new AuthSessionDto(
                issued.Value.AccessToken,
                issued.Value.RefreshToken,
                issued.Value.AccessTokenExpiresAt,
                issued.Value.RefreshTokenExpiresAt,
                issued.Value.User);
        }

        return Result.Success(new AcceptTenantInvitationResponse(
            tenant.Id,
            tenant.Slug,
            tenant.Name,
            invitation.Role.ToString(),
            session));
    }

    private async Task<Result<ResolvedUser>> ResolveUserAsync(
        TenantInvitation invitation,
        AcceptTenantInvitationCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.IsAuthenticated)
        {
            if (currentUser.UserId is not { } authenticatedUserId)
            {
                return Result.Failure<ResolvedUser>(Error.Unauthorized(
                    "auth.required",
                    "Authentication is required."));
            }

            if (!string.Equals(
                    currentUser.Email?.Trim(),
                    invitation.Email,
                    StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<ResolvedUser>(Error.Forbidden(
                    "invitation.email_mismatch",
                    "The signed-in account does not match the invited email address."));
            }

            return Result.Success(new ResolvedUser(
                authenticatedUserId,
                currentUser.Actor.Name,
                IsNewUser: false));
        }

        var existingUser = await identity.FindByEmailAsync(invitation.Email, cancellationToken);
        if (existingUser.IsSuccess)
        {
            return Result.Failure<ResolvedUser>(Error.Unauthorized(
                "invitation.login_required",
                "Sign in with the invited email address to accept this invitation."));
        }

        var displayName = command.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return Result.Failure<ResolvedUser>(Error.Validation(
                "invitation.display_name_required",
                "A display name is required to create an account."));
        }

        if (displayName.Length > 160)
        {
            return Result.Failure<ResolvedUser>(Error.Validation(
                "invitation.invalid_display_name",
                "Display name must be at most 160 characters."));
        }

        if (string.IsNullOrWhiteSpace(command.Password))
        {
            return Result.Failure<ResolvedUser>(Error.Validation(
                "invitation.password_required",
                "A password is required to create an account."));
        }

        var created = await identity.CreateUserAsync(
            invitation.Email,
            command.Password,
            displayName,
            cancellationToken);

        if (created.IsFailure)
        {
            return Result.Failure<ResolvedUser>(created.Error!);
        }

        return Result.Success(new ResolvedUser(
            created.Value.Id,
            created.Value.DisplayName,
            IsNewUser: true));
    }

    private sealed record ResolvedUser(Guid UserId, string DisplayName, bool IsNewUser);
}
