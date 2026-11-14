namespace PaymentReconciliation.Modules.Tenancy.Contracts;

public interface ITenantDirectory
{
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> FindIdsByNameAsync(
        string search,
        CancellationToken cancellationToken);
}
