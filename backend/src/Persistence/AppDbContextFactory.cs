using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PaymentReconciliation.Persistence.Abstractions;
using PaymentReconciliation.SharedKernel.Abstractions;

namespace PaymentReconciliation.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=payment_reconciliation;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new TenantContext(), new TenantBoundaryBypass());
    }
}
