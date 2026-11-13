namespace PaymentReconciliation.Modules.Tenancy.Application;

public sealed record TenantSettingsDto(
    Guid Id,
    string Name,
    string Slug,
    string Status,
    string Role,
    int MemberCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record TenantMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset JoinedAt);

public sealed record RenameTenantRequest(string Name);

public sealed record AddTenantMemberRequest(string Email, string Role);

public sealed record ChangeTenantMemberRoleRequest(string Role);
