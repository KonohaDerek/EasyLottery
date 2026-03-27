namespace EasyLotteryDomain.Models.Config
{
    public sealed class EasyLotteryBackupPackage
    {
        public int BackupVersion { get; set; } = 1;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public EasyLotteryConfigDocument Document { get; set; } = new();
    }
}
