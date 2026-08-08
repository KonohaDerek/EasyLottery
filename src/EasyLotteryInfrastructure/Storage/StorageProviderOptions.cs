namespace EasyLotteryInfrastructure.Storage;

public sealed class StorageProviderOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "yaml";

    public string? ConnectionString { get; set; }

    public string NormalizedProvider => string.IsNullOrWhiteSpace(Provider)
        ? "yaml"
        : Provider.Trim().ToLowerInvariant();

    public void Validate()
    {
        if (NormalizedProvider is not ("yaml" or "sqlite" or "postgresql"))
            throw new InvalidOperationException($"Storage:Provider 不支援 '{Provider}'。可用值為 yaml、sqlite 或 postgresql。");

        if (NormalizedProvider == "sqlite" && string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("使用 sqlite 儲存提供者時必須設定 Storage:ConnectionString。");
    }

    public void ValidateAdapterAvailability()
    {
        if (NormalizedProvider == "postgresql")
            throw new InvalidOperationException("Storage:Provider=postgresql 尚未註冊 adapter；目前可用 provider 為 yaml 與 sqlite。");
    }
}
