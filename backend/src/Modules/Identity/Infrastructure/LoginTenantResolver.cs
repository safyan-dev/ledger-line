using Microsoft.AspNetCore.Http;
using PaymentReconciliation.Modules.Identity.Application;

namespace PaymentReconciliation.Modules.Identity.Infrastructure;

public sealed class LoginTenantResolver(IHttpContextAccessor httpContextAccessor) : ILoginTenantResolver
{
    public string? ResolveTenantSlug()
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        var header = context.Request.Headers["X-Tenant-Slug"].ToString().Trim();
        if (header.Length > 0)
        {
            return Normalize(header);
        }

        var host = context.Request.Host.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var normalizedHost = host.ToLowerInvariant();
        if (normalizedHost is "localhost" or "127.0.0.1" or "::1")
        {
            return null;
        }

        var labels = normalizedHost.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (labels.Length < 2)
        {
            return null;
        }

        var candidate = labels[0];
        if (candidate is "www" or "app" or "api")
        {
            return null;
        }

        return Normalize(candidate);
    }

    private static string? Normalize(string value)
    {
        var slug = value.Trim().ToLowerInvariant();

        if (slug.Length is < 2 or > 40)
        {
            return null;
        }

        if (!slug.All(character => char.IsLetterOrDigit(character) || character == '-'))
        {
            return null;
        }

        return char.IsLetterOrDigit(slug[0]) && char.IsLetterOrDigit(slug[^1]) ? slug : null;
    }
}
