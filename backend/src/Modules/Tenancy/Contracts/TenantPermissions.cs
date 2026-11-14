using PaymentReconciliation.SharedKernel.Results;

namespace PaymentReconciliation.Modules.Tenancy.Contracts;

public enum TenantPermission
{
    ViewTeam,
    ManageTeam,
    InviteTeamMembers,
    ManageWorkspaceSettings,
    ViewSubscription,
    ManageSubscription,
    ViewUsage,
    ViewFinancials,
    RunReconciliation,
    CreateSupportTicket,
    ViewSupportTicket,
    ReplySupportTicket,
    ResolveSupportTicket,
    ManageOwnAccount
}

public interface ITenantAuthorizer
{
    bool IsGranted(TenantPermission permission);

    Result Require(TenantPermission permission);
}
