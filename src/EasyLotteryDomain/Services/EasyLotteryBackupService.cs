using System.Text.Json;
using EasyLotteryDomain.Models.Config;

namespace EasyLotteryDomain.Services
{
    public sealed class EasyLotteryBackupService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly IEasyLotteryConfigStore _configStore;

        public EasyLotteryBackupService(IEasyLotteryConfigStore configStore)
        {
            _configStore = configStore;
        }

        public async Task<EasyLotteryBackupPackage> CreateBackupAsync(CancellationToken cancellationToken = default)
        {
            var document = await _configStore.LoadAsync(cancellationToken);

            return new EasyLotteryBackupPackage
            {
                BackupVersion = 1,
                CreatedAtUtc = DateTime.UtcNow,
                Document = document
            };
        }

        public async Task RestoreAsync(string json, CancellationToken cancellationToken = default)
        {
            if (TryDeserializeBackupPackage(json, out var package))
            {
                await _configStore.SaveAsync(package!.Document, cancellationToken);
                return;
            }

            var legacyDocument = JsonSerializer.Deserialize<EasyLotteryConfigDocument>(json, JsonOptions);
            if (legacyDocument is null)
            {
                throw new InvalidOperationException("無法解析備份內容。");
            }

            await _configStore.SaveAsync(legacyDocument, cancellationToken);
        }

        public string SerializeBackup(EasyLotteryBackupPackage package)
        {
            return JsonSerializer.Serialize(package, JsonOptions);
        }

        public static bool TryDeserializeBackupPackage(string json, out EasyLotteryBackupPackage? package)
        {
            package = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using var jsonDocument = JsonDocument.Parse(json);
                var root = jsonDocument.RootElement;
                if (root.ValueKind != JsonValueKind.Object || !HasProperty(root, "document"))
                {
                    return false;
                }

                package = JsonSerializer.Deserialize<EasyLotteryBackupPackage>(json, JsonOptions);
                if (package is null || package.BackupVersion <= 0 || package.Document is null)
                {
                    package = null;
                    return false;
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool HasProperty(JsonElement root, string propertyName)
        {
            foreach (var property in root.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
