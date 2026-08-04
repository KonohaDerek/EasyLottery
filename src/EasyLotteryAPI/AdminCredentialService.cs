using System.Security.Cryptography;
using System.Text;

namespace EasyLotteryApi;

public sealed class AdminCredentialService
{
    private readonly byte[] _passwordBytes;
    public string SourceDescription { get; }

    public AdminCredentialService(IConfiguration configuration, IWebHostEnvironment environment, ILogger<AdminCredentialService> logger)
    {
        var configured = configuration["Security:AdminPassword"] ?? Environment.GetEnvironmentVariable("EASYLOTTERY_ADMIN_PASSWORD");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            _passwordBytes = Encoding.UTF8.GetBytes(configured);
            SourceDescription = "Security:AdminPassword / EASYLOTTERY_ADMIN_PASSWORD";
            return;
        }

        var storageDirectory = configuration["Storage:Directory"] ?? Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(storageDirectory);
        var passwordPath = Path.Combine(storageDirectory, ".admin-password");
        if (!File.Exists(passwordPath))
        {
            File.WriteAllText(passwordPath, Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant());
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(passwordPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        _passwordBytes = Encoding.UTF8.GetBytes(File.ReadAllText(passwordPath).Trim());
        SourceDescription = passwordPath;
        logger.LogWarning("Admin password is stored at {AdminPasswordPath}. Keep this file private.", passwordPath);
    }

    public bool Verify(string? password)
    {
        var supplied = Encoding.UTF8.GetBytes(password ?? "");
        return supplied.Length == _passwordBytes.Length && CryptographicOperations.FixedTimeEquals(supplied, _passwordBytes);
    }
}
