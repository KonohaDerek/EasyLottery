using System.Security.Cryptography;
using System.Text;

namespace EasyLotteryCardHub.Security;

public sealed class IdentityHasher(IConfiguration configuration)
{
    public byte[] HashEmail(string normalizedEmail)
    {
        return HMACSHA256.HashData(ReadKey(), Encoding.UTF8.GetBytes(normalizedEmail));
    }

    public static void ValidateConfiguration(IConfiguration configuration) => _ = ReadKey(configuration);

    private byte[] ReadKey() => ReadKey(configuration);

    private static byte[] ReadKey(IConfiguration configuration)
    {
        var encodedKey = configuration["CardHub:Identity:HmacKey"];
        if (string.IsNullOrWhiteSpace(encodedKey))
            throw new InvalidOperationException("CardHub:Identity:HmacKey must be configured before processing email identities.");
        byte[] key;
        try { key = Convert.FromBase64String(encodedKey); }
        catch (FormatException exception) { throw new InvalidOperationException("CardHub:Identity:HmacKey must be base64-encoded.", exception); }
        if (key.Length < 32) throw new InvalidOperationException("CardHub:Identity:HmacKey must contain at least 32 bytes.");
        return key;
    }
}
