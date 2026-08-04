namespace EasyLotteryDomain.Models.Obs;

/// <summary>媒體資產在 OBS 中的用途。實際檔案由 Infrastructure 負責保存。</summary>
public enum ObsAssetKind
{
    Image,
    Audio,
    Model,
    Other
}

/// <summary>
/// OBS 可離線使用的本機資產描述。Metadata 與二進位內容分開保存，讓資產可被替換而不影響活動設定。
/// </summary>
public sealed class ObsAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = "asset";
    public string ContentType { get; set; } = "application/octet-stream";
    public ObsAssetKind Kind { get; set; } = ObsAssetKind.Other;
    public long Length { get; set; }
    public string Sha256 { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    /// <summary>使用此資產的資源識別，例如 donate:{publicId} 或 sound-cue:classic。</summary>
    public List<string> ReferencedBy { get; set; } = [];
}
