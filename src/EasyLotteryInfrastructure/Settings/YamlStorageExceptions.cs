namespace EasyLotteryInfrastructure.Settings;

public sealed class YamlStorageException : IOException
{
    public YamlStorageException(string message, string storagePath, string? quarantinedPath = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StoragePath = storagePath;
        QuarantinedPath = quarantinedPath;
    }

    public string StoragePath { get; }

    public string? QuarantinedPath { get; }
}

public sealed class ConfigurationConcurrencyException : InvalidOperationException
{
    public ConfigurationConcurrencyException(string expectedETag, string actualETag)
        : base("設定已被其他用戶更新，請重新載入後再儲存。")
    {
        ExpectedETag = expectedETag;
        ActualETag = actualETag;
    }

    public string ExpectedETag { get; }

    public string ActualETag { get; }
}

public sealed record StorageBackupInfo(string FileName, DateTimeOffset CreatedAtUtc, long Length);
