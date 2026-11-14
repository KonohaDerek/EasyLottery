using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace EasyLotteryCardHub.Security;

public static class SiteSecret
{
    public static (string ClientId, string Secret, byte[] Hash) Create()
    {
        var clientId = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(18));
        var secret = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (clientId, secret, Hash(secret));
    }

    public static byte[] Hash(string secret) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));

    public static bool Verify(string suppliedSecret, ReadOnlySpan<byte> expectedHash)
    {
        var actualHash = Hash(suppliedSecret);
        return actualHash.Length == expectedHash.Length && CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
