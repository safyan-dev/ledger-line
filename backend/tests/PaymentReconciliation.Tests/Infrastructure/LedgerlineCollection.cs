using Xunit;

namespace PaymentReconciliation.Tests.Infrastructure;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LedgerlineCollection : ICollectionFixture<LedgerlineAppFactory>
{
    public const string Name = "ledgerline-api";
}
