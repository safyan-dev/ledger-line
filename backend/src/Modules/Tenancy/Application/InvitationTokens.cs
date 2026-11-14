using System.Security.Cryptography;
using System.Text;

namespace PaymentReconciliation.Modules.Tenancy.Application;

internal static class InvitationTokens
{
    public static string Create()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    public static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
