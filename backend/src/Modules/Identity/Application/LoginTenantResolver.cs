namespace PaymentReconciliation.Modules.Identity.Application;

public interface ILoginTenantResolver
{
    string? ResolveTenantSlug();
}
