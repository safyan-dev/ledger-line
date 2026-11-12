using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Api;

public sealed class DemoCurrentUser : ICurrentUser
{
    private static readonly AuditActor DemoActor = new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        "Avery Chen",
        "Operations");

    public AuditActor Actor => DemoActor;
}
