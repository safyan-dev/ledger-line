namespace PaymentReconciliation.SharedKernel.Abstractions;

public sealed record AuditActor(Guid? Id, string Name, string Role)
{
    public static AuditActor System { get; } = new(null, "system", "System");
}

public interface ICurrentUser
{
    AuditActor Actor { get; }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
