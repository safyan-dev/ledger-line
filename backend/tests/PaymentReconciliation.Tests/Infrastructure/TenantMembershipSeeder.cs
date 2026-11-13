using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentReconciliation.Modules.Tenancy.Domain;
using PaymentReconciliation.Persistence;

namespace PaymentReconciliation.Tests.Infrastructure;

public static class TenantMembershipSeeder
{
    public static async Task AddMembershipAsync(
        this LedgerlineAppFactory factory,
        Guid tenantId,
        Guid userId,
        TenantRole role = TenantRole.Finance)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var exists = await dbContext.Set<TenantUser>()
            .AnyAsync(membership => membership.TenantId == tenantId && membership.UserId == userId);

        if (exists)
        {
            return;
        }

        dbContext.Set<TenantUser>().Add(TenantUser.Create(tenantId, userId, role));
        await dbContext.SaveChangesAsync();
    }
}
