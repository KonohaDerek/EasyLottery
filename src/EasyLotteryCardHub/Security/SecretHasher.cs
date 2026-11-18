using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace EasyLotteryCardHub.Security;

public static class SecretHasher
{
    public static string CreateInviteToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string CreateSiteSecret() => "chub_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string CreateClientId() => "chub_site_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(18));

    public static string Hash(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    public static bool Verify(string secret, string expectedHash)
    {
        var candidate = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            expected = new byte[SHA256.HashSizeInBytes];
        }

        return expected.Length == candidate.Length && CryptographicOperations.FixedTimeEquals(candidate, expected);
    }
}
