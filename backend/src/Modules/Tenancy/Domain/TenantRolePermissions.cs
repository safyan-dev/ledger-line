using PaymentReconciliation.Modules.Tenancy.Contracts;

namespace PaymentReconciliation.Modules.Tenancy.Domain;

public static class TenantRolePermissions
{
    private static readonly IReadOnlyDictionary<TenantRole, HashSet<TenantPermission>> Grants =
        new Dictionary<TenantRole, HashSet<TenantPermission>>
        {
            [TenantRole.Owner] = [.. Enum.GetValues<TenantPermission>()],
            [TenantRole.TenantAdmin] =
            [
                TenantPermission.ViewTeam,
                TenantPermission.ManageTeam,
                TenantPermission.InviteTeamMembers,
                TenantPermission.ManageWorkspaceSettings,
                TenantPermission.ViewSubscription,
                TenantPermission.ViewUsage,
                TenantPermission.ViewFinancials,
                TenantPermission.RunReconciliation,
                TenantPermission.CreateSupportTicket,
                TenantPermission.ViewSupportTicket,
                TenantPermission.ReplySupportTicket,
                TenantPermission.ManageOwnAccount
            ],
            [TenantRole.Finance] =
            [
                TenantPermission.ViewTeam,
                TenantPermission.ViewSubscription,
                TenantPermission.ViewUsage,
                TenantPermission.ViewFinancials,
                TenantPermission.RunReconciliation,
                TenantPermission.CreateSupportTicket,
                TenantPermission.ViewSupportTicket,
                TenantPermission.ReplySupportTicket,
                TenantPermission.ManageOwnAccount
            ]
        };

    public static bool IsGranted(TenantRole role, TenantPermission permission)
        => Grants.TryGetValue(role, out var permissions) && permissions.Contains(permission);

    public static IReadOnlyCollection<TenantPermission> For(TenantRole role)
        => Grants.TryGetValue(role, out var permissions) ? permissions : [];
}
