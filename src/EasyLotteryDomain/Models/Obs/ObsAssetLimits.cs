namespace EasyLotteryDomain.Models.Obs;

public static class ObsAssetLimits
{
    public const long MinimumAssetBytes = 64 * 1024;
    public const long DefaultMaxAssetBytes = 10 * 1024 * 1024;
    public const long MaximumConfiguredAssetBytes = 50 * 1024 * 1024;
    public const long MaximumPackageBytes = 100 * 1024 * 1024;
    public const long RequestOverheadBytes = 1 * 1024 * 1024;

    public static long ReadMaxAssetBytes(string? configured) =>
        long.TryParse(configured, out var value)
            ? Math.Clamp(value, MinimumAssetBytes, MaximumConfiguredAssetBytes)
            : DefaultMaxAssetBytes;
}

public sealed record ObsAssetLimitValues(long MaxAssetBytes, long MaxPackageBytes);
