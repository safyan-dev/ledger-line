using Microsoft.EntityFrameworkCore;
using PaymentReconciliation.Modules.Identity.Contracts;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;
using PaymentReconciliation.SharedKernel.Cqrs;
using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record GetCurrentTenantQuery : IQuery<TenantSettingsDto>;

public sealed class GetCurrentTenantHandler(IAppDbContext dbContext, ITenantContext tenantContext)
    : IQueryHandler<GetCurrentTenantQuery, TenantSettingsDto>
{
    public async Task<Result<TenantSettingsDto>> Handle(
        GetCurrentTenantQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TenantSettingsDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var tenant = await dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantSettingsDto>(Error.NotFound(
                "tenant.not_found",
                "Tenant was not found."));
        }

        var memberCount = await dbContext.Set<TenantUser>()
            .CountAsync(membership => membership.TenantId == tenantId, cancellationToken);

        return Result.Success(new TenantSettingsDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenantContext.Role ?? string.Empty,
            memberCount,
            tenant.CreatedAt,
            tenant.UpdatedAt));
    }
}

public sealed record RenameTenantCommand(string Name) : ICommand<TenantSettingsDto>;

public sealed class RenameTenantHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : ICommandHandler<RenameTenantCommand, TenantSettingsDto>
{
    public async Task<Result<TenantSettingsDto>> Handle(
        RenameTenantCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TenantSettingsDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        if (!TenantRoleRules.TryParse(tenantContext.Role, out var actorRole)
            || !TenantRoleRules.CanManageTenant(actorRole))
        {
            return Result.Failure<TenantSettingsDto>(Error.Forbidden(
                "tenant.forbidden",
                "Only an owner or admin can change tenant settings."));
        }

        var tenant = await dbContext.Set<Tenant>()
            .FirstOrDefaultAsync(candidate => candidate.Id == tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<TenantSettingsDto>(Error.NotFound(
                "tenant.not_found",
                "Tenant was not found."));
        }

        tenant.Rename(command.Name, currentUser.Actor);

        var memberCount = await dbContext.Set<TenantUser>()
            .CountAsync(membership => membership.TenantId == tenantId, cancellationToken);

        return Result.Success(new TenantSettingsDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString(),
            tenantContext.Role ?? string.Empty,
            memberCount,
            tenant.CreatedAt,
            tenant.UpdatedAt));
    }
}

public sealed record GetTenantMembersQuery : IQuery<IReadOnlyList<TenantMemberDto>>;

public sealed class GetTenantMembersHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    IIdentityFacade identity)
    : IQueryHandler<GetTenantMembersQuery, IReadOnlyList<TenantMemberDto>>
{
    public async Task<Result<IReadOnlyList<TenantMemberDto>>> Handle(
        GetTenantMembersQuery query,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<TenantMemberDto>>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        var memberships = await dbContext.Set<TenantUser>()
            .AsNoTracking()
            .Where(membership => membership.TenantId == tenantId)
            .OrderBy(membership => membership.CreatedAt)
            .ToListAsync(cancellationToken);

        var users = await identity.GetUsersAsync(
            memberships.Select(membership => membership.UserId).Distinct().ToList(),
            cancellationToken);
        var usersById = users.ToDictionary(user => user.Id);

        var members = memberships
            .Where(membership => usersById.ContainsKey(membership.UserId))
            .Select(membership =>
            {
                var user = usersById[membership.UserId];
                return new TenantMemberDto(
                    membership.UserId,
                    user.Email,
                    user.DisplayName,
                    membership.Role.ToString(),
                    membership.CreatedAt);
            })
            .ToList();

        return Result.Success<IReadOnlyList<TenantMemberDto>>(members);
    }
}

public sealed record AddTenantMemberCommand(string Email, string Role) : ICommand<TenantMemberDto>;

public sealed class AddTenantMemberHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IIdentityFacade identity)
    : ICommandHandler<AddTenantMemberCommand, TenantMemberDto>
{
    public async Task<Result<TenantMemberDto>> Handle(
        AddTenantMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TenantMemberDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        if (!TenantRoleRules.TryParse(tenantContext.Role, out var actorRole)
            || !TenantRoleRules.CanManageTenant(actorRole))
        {
            return Result.Failure<TenantMemberDto>(Error.Forbidden(
                "tenant.forbidden",
                "Only an owner or admin can add team members."));
        }

        if (!TenantRoleParsing.TryParseRole(command.Role, out var role))
        {
            return Result.Failure<TenantMemberDto>(Error.Validation(
                "tenant.invalid_role",
                "Role must be Owner, Admin or Finance."));
        }

        if (!TenantRoleRules.CanAssignRole(actorRole, role))
        {
            return Result.Failure<TenantMemberDto>(Error.Forbidden(
                "tenant.role_forbidden",
                "Only an owner can assign the Owner role."));
        }

        var user = await identity.FindByEmailAsync(command.Email, cancellationToken);
        if (user.IsFailure)
        {
            return Result.Failure<TenantMemberDto>(user.Error!);
        }

        var existing = await dbContext.Set<TenantUser>()
            .FirstOrDefaultAsync(
                membership => membership.TenantId == tenantId && membership.UserId == user.Value.Id,
                cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<TenantMemberDto>(Error.Conflict(
                "tenant.member_exists",
                "This user is already a member of the tenant."));
        }

        var membership = TenantUser.Add(tenantId, user.Value.Id, role, currentUser.Actor);
        dbContext.Set<TenantUser>().Add(membership);

        return Result.Success(new TenantMemberDto(
            user.Value.Id,
            user.Value.Email,
            user.Value.DisplayName,
            role.ToString(),
            membership.CreatedAt));
    }
}

public sealed record ChangeTenantMemberRoleCommand(Guid UserId, string Role) : ICommand<TenantMemberDto>;

public sealed class ChangeTenantMemberRoleHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser,
    IIdentityFacade identity)
    : ICommandHandler<ChangeTenantMemberRoleCommand, TenantMemberDto>
{
    public async Task<Result<TenantMemberDto>> Handle(
        ChangeTenantMemberRoleCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<TenantMemberDto>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        if (!TenantRoleRules.TryParse(tenantContext.Role, out var actorRole)
            || !TenantRoleRules.CanManageTenant(actorRole))
        {
            return Result.Failure<TenantMemberDto>(Error.Forbidden(
                "tenant.forbidden",
                "Only an owner or admin can change member roles."));
        }

        if (!TenantRoleParsing.TryParseRole(command.Role, out var role))
        {
            return Result.Failure<TenantMemberDto>(Error.Validation(
                "tenant.invalid_role",
                "Role must be Owner, Admin or Finance."));
        }

        var membership = await dbContext.Set<TenantUser>()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.UserId == command.UserId,
                cancellationToken);

        if (membership is null)
        {
            return Result.Failure<TenantMemberDto>(Error.NotFound(
                "tenant.member_not_found",
                "The team member was not found."));
        }

        if (membership.Role == TenantRole.Owner && actorRole != TenantRole.Owner)
        {
            return Result.Failure<TenantMemberDto>(Error.Forbidden(
                "tenant.role_forbidden",
                "Only an owner can change another owner's role."));
        }

        if (!TenantRoleRules.CanAssignRole(actorRole, role))
        {
            return Result.Failure<TenantMemberDto>(Error.Forbidden(
                "tenant.role_forbidden",
                "Only an owner can assign the Owner role."));
        }

        if (membership.Role == TenantRole.Owner && role != TenantRole.Owner)
        {
            var ownerCount = await dbContext.Set<TenantUser>()
                .CountAsync(
                    candidate => candidate.TenantId == tenantId && candidate.Role == TenantRole.Owner,
                    cancellationToken);

            if (ownerCount <= 1)
            {
                return Result.Failure<TenantMemberDto>(Error.Conflict(
                    "tenant.last_owner",
                    "A tenant must keep at least one owner."));
            }
        }

        membership.ChangeRole(role, currentUser.Actor);

        var users = await identity.GetUsersAsync([membership.UserId], cancellationToken);
        var user = users.FirstOrDefault();

        return Result.Success(new TenantMemberDto(
            membership.UserId,
            user?.Email ?? string.Empty,
            user?.DisplayName ?? string.Empty,
            membership.Role.ToString(),
            membership.CreatedAt));
    }
}

public sealed record RemoveTenantMemberCommand(Guid UserId) : ICommand<Guid>;

public sealed class RemoveTenantMemberHandler(
    IAppDbContext dbContext,
    ITenantContext tenantContext,
    ICurrentUser currentUser)
    : ICommandHandler<RemoveTenantMemberCommand, Guid>
{
    public async Task<Result<Guid>> Handle(
        RemoveTenantMemberCommand command,
        CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<Guid>(Error.Forbidden(
                "tenant.required",
                "A tenant context is required."));
        }

        if (!TenantRoleRules.TryParse(tenantContext.Role, out var actorRole)
            || !TenantRoleRules.CanManageTenant(actorRole))
        {
            return Result.Failure<Guid>(Error.Forbidden(
                "tenant.forbidden",
                "Only an owner or admin can remove team members."));
        }

        var membership = await dbContext.Set<TenantUser>()
            .FirstOrDefaultAsync(
                candidate => candidate.TenantId == tenantId && candidate.UserId == command.UserId,
                cancellationToken);

        if (membership is null)
        {
            return Result.Failure<Guid>(Error.NotFound(
                "tenant.member_not_found",
                "The team member was not found."));
        }

        if (membership.Role == TenantRole.Owner)
        {
            if (actorRole != TenantRole.Owner)
            {
                return Result.Failure<Guid>(Error.Forbidden(
                    "tenant.role_forbidden",
                    "Only an owner can remove another owner."));
            }

            var ownerCount = await dbContext.Set<TenantUser>()
                .CountAsync(
                    candidate => candidate.TenantId == tenantId && candidate.Role == TenantRole.Owner,
                    cancellationToken);

            if (ownerCount <= 1)
            {
                return Result.Failure<Guid>(Error.Conflict(
                    "tenant.last_owner",
                    "A tenant must keep at least one owner."));
            }
        }

        membership.Remove(currentUser.Actor);
        dbContext.Set<TenantUser>().Remove(membership);

        return Result.Success(membership.UserId);
    }
}

internal static class TenantRoleParsing
{
    public static bool TryParseRole(string? value, out TenantRole role)
    {
        role = default;
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        var match = Enum.GetNames<TenantRole>()
            .FirstOrDefault(name => string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase));

        return match is not null && Enum.TryParse(match, out role);
    }
}
