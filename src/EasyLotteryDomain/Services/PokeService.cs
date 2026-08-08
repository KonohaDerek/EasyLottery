using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryDomain.Services
{
    public class PokeService
    {
        private readonly IEasyLotteryConfigStore _configStore;
        private readonly EasyLotteryAuditService? _auditService;

        private static readonly Random _random = Random.Shared;

        public PokeService(IEasyLotteryConfigStore configStore, EasyLotteryAuditService? auditService = null)
        {
            _configStore = configStore;
            _auditService = auditService;
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        public async Task<PokeTemplate> CreateTemplateAsync(PokeTemplate template)
        {
            var document = await _configStore.LoadAsync();
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            template.Id = document.IdSequence.NextPokeTemplateId++;
            template.PublicId = Guid.NewGuid();
            PrepareTemplateForSave(template, document.IdSequence.NextPokeCellId);
            document.IdSequence.NextPokeCellId = Math.Max(document.IdSequence.NextPokeCellId, template.Cells.Select(c => c.Id).DefaultIfEmpty(document.IdSequence.NextPokeCellId - 1).Max() + 1);
            document.PokeTemplates.Add(template);
            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, "建立戳戳樂模板", template.Name, $"模板 ID {template.Id}");
            return template;
        }

        public async Task UpdateTemplateAsync(PokeTemplate template)
        {
            var document = await _configStore.LoadAsync();
            template.UpdatedAt = DateTime.UtcNow;

            var existing = document.PokeTemplates
                .FirstOrDefault(t => t.Id == template.Id)
                ?? throw new InvalidOperationException($"Template {template.Id} not found.");
            var beforeJson = EasyLotteryAuditService.Snapshot(existing);

            template.CreatedAt = existing.CreatedAt;
            template.PublicId = existing.PublicId != Guid.Empty
                ? existing.PublicId
                : template.PublicId != Guid.Empty ? template.PublicId : Guid.NewGuid();
            PrepareTemplateForSave(template, document.IdSequence.NextPokeCellId);
            document.IdSequence.NextPokeCellId = Math.Max(document.IdSequence.NextPokeCellId, template.Cells.Select(c => c.Id).DefaultIfEmpty(document.IdSequence.NextPokeCellId - 1).Max() + 1);

            var existingIndex = document.PokeTemplates.FindIndex(t => t.Id == template.Id);
            document.PokeTemplates[existingIndex] = template;
            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, "更新戳戳樂模板", template.Name, $"模板 ID {template.Id}", beforeJson, EasyLotteryAuditService.Snapshot(template));
        }

        public async Task DeleteTemplateAsync(int id)
        {
            var document = await _configStore.LoadAsync();
            var template = document.PokeTemplates.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"Template {id} not found.");
            document.PokeTemplates.Remove(template);
            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, "刪除戳戳樂模板", template.Name, $"模板 ID {template.Id}");
        }

        public async Task<PokeTemplate> DuplicateTemplateAsync(int id)
        {
            var document = await _configStore.LoadAsync();
            var source = document.PokeTemplates.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"Template {id} not found.");

            var duplicate = new PokeTemplate
            {
                Name = BuildDuplicateName(source.Name, document.PokeTemplates.Select(t => t.Name)),
                Description = source.Description,
                GridRows = source.GridRows,
                GridColumns = source.GridColumns,
                Mode = source.Mode,
                AllowRePoking = source.AllowRePoking,
                MaxPokeCount = source.MaxPokeCount,
                BackgroundImageUrl = source.BackgroundImageUrl,
                FontFamily = source.FontFamily,
                CongratulationMessage = source.CongratulationMessage,
                OverlayWidth = source.OverlayWidth,
                OverlayHeight = source.OverlayHeight,
                Animation = source.Animation,
                AnimationDurationMs = source.AnimationDurationMs,
                ResultDisplayDurationSeconds = source.ResultDisplayDurationSeconds,
                PokeSoundUrl = source.PokeSoundUrl,
                OpenSoundUrl = source.OpenSoundUrl,
                IsBuiltIn = false,
                PublicationStatus = TemplatePublicationStatus.Draft,
                Cells = source.Cells
                    .OrderBy(cell => cell.Index)
                    .Select(cell => new PokeCell
                    {
                        Index = cell.Index,
                        Title = cell.Title,
                        SubTitle = cell.SubTitle,
                        ImageUrl = cell.ImageUrl,
                        RevealedImageUrl = cell.RevealedImageUrl,
                        RevealedColor = cell.RevealedColor,
                        IsRevealed = false,
                        RevealedAt = null
                    })
                    .ToList()
            };

            var created = await CreateTemplateAsync(duplicate);
            await RecordAuditAsync(document, "複製戳戳樂模板", created.Name, $"來源模板 ID {source.Id}");
            return created;
        }

        public async Task<List<PokeTemplate>> ListTemplatesAsync()
        {
            var document = await LoadDocumentAsync(ensureBuiltIns: true);
            return document.PokeTemplates
                .OrderBy(t => t.PublicationStatus)
                .ThenByDescending(t => t.UpdatedAt)
                .ToList();
        }

        public async Task<PokeTemplate?> LoadTemplateAsync(int id)
        {
            var document = await LoadDocumentAsync(ensureBuiltIns: true);
            return document.PokeTemplates
                .FirstOrDefault(t => t.Id == id);
        }

        public async Task<PokeTemplate?> LoadTemplateAsync(Guid publicId)
        {
            var document = await LoadDocumentAsync(ensureBuiltIns: true);
            return document.PokeTemplates.FirstOrDefault(t => t.PublicId == publicId);
        }

        public async Task<PokeTemplate> SetPublicationStatusAsync(int id, TemplatePublicationStatus status)
        {
            var document = await _configStore.LoadAsync();
            var template = document.PokeTemplates.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"Template {id} not found.");

            template.PublicationStatus = status;
            template.UpdatedAt = DateTime.UtcNow;
            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, status == TemplatePublicationStatus.Published ? "發布戳戳樂模板" : "轉為戳戳樂草稿", template.Name, $"模板 ID {template.Id}");
            return template;
        }

        // ── Poke Logic ────────────────────────────────────────────────────────

        /// <summary>Randomly poke one unrevealed cell. Returns null if all cells are already revealed.</summary>
        public async Task<PokeCell?> PokeRandomCellAsync(int templateId)
        {
            var document = await _configStore.LoadAsync();
            var template = document.PokeTemplates
                .FirstOrDefault(t => t.Id == templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            if (!CanPoke(template)) return null;

            var unrevealed = template.Cells.Where(c => !c.IsRevealed).ToList();
            if (unrevealed.Count == 0) return null;

            var cell = unrevealed[_random.Next(unrevealed.Count)];
            cell.IsRevealed = true;
            cell.RevealedAt = DateTime.UtcNow;
            await _configStore.SaveAsync(document);
            return cell;
        }

        /// <summary>Manually poke a specific cell by its index.</summary>
        public async Task<PokeCell?> PokeCellByIndexAsync(int templateId, int index)
        {
            var document = await _configStore.LoadAsync();
            var template = document.PokeTemplates
                .FirstOrDefault(t => t.Id == templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            var cell = template.Cells.FirstOrDefault(c => c.Index == index)
                ?? throw new InvalidOperationException($"Cell at index {index} not found.");

            if (cell.IsRevealed && !template.AllowRePoking) return null;
            if (!CanPoke(template)) return null;

            cell.IsRevealed = true;
            cell.RevealedAt = DateTime.UtcNow;
            await _configStore.SaveAsync(document);
            return cell;
        }

        /// <summary>Returns the current reveal state for all cells in the template.</summary>
        public async Task<List<PokeCell>> GetRevealStateAsync(int templateId)
        {
            var document = await _configStore.LoadAsync();
            return document.PokeTemplates
                .FirstOrDefault(t => t.Id == templateId)?
                .Cells
                .OrderBy(c => c.Index)
                .ToList() ?? new List<PokeCell>();
        }

        // ── Reset ─────────────────────────────────────────────────────────────

        public async Task ResetTemplateAsync(int templateId)
        {
            var document = await _configStore.LoadAsync();
            var template = document.PokeTemplates.FirstOrDefault(t => t.Id == templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            foreach (var cell in template.Cells)
            {
                cell.IsRevealed = false;
                cell.RevealedAt = null;
            }
            await _configStore.SaveAsync(document);
        }

        // ── Export / Import ───────────────────────────────────────────────────

        public async Task<string> ExportTemplateAsync(int templateId)
        {
            var template = await LoadTemplateAsync(templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            // Remove navigation references that would cause circular references
            foreach (var cell in template.Cells)
                cell.Template = null!;

            return JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task<PokeTemplate> ImportTemplateAsync(string json)
        {
            var template = JsonSerializer.Deserialize<PokeTemplate>(json)
                ?? throw new ArgumentException("Invalid template JSON.");

            // Reset IDs so EF treats them as new records
            template.Id = 0;
            template.PublicId = Guid.Empty;
            template.IsBuiltIn = false;
            template.PublicationStatus = TemplatePublicationStatus.Draft;
            foreach (var cell in template.Cells)
            {
                cell.Id = 0;
                cell.TemplateId = 0;
                cell.IsRevealed = false;
                cell.RevealedAt = null;
            }

            return await CreateTemplateAsync(template);
        }

        // ── Default Templates ─────────────────────────────────────────────────

        /// <summary>Seeds 3 built-in default templates if they do not yet exist.</summary>
        public async Task SeedDefaultTemplatesAsync()
        {
            var document = await _configStore.LoadAsync();
            if (!EnsureBuiltInTemplates(document))
            {
                return;
            }

            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, "建立內建戳戳樂模板", "預設模板", "初始化 3 組內建模板");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool CanPoke(PokeTemplate template)
        {
            if (template.MaxPokeCount <= 0) return true;
            var revealed = template.Cells.Count(c => c.IsRevealed);
            return revealed < template.MaxPokeCount;
        }

        private async Task<EasyLotteryDomain.Models.Config.EasyLotteryConfigDocument> LoadDocumentAsync(bool ensureBuiltIns)
        {
            var document = await _configStore.LoadAsync();
            var changed = false;
            if (ensureBuiltIns && EnsureBuiltInTemplates(document))
            {
                changed = true;
            }
            changed |= EnsurePublicIds(document);

            if (changed)
            {
                await _configStore.SaveAsync(document);
            }

            return document;
        }

        private static bool EnsurePublicIds(EasyLotteryDomain.Models.Config.EasyLotteryConfigDocument document)
        {
            var changed = false;
            foreach (var template in document.PokeTemplates.Where(template => template.PublicId == Guid.Empty))
            {
                template.PublicId = Guid.NewGuid();
                changed = true;
            }

            return changed;
        }

        private static bool EnsureBuiltInTemplates(EasyLotteryDomain.Models.Config.EasyLotteryConfigDocument document)
        {
            if (document.PokeTemplates.Any(t => t.IsBuiltIn))
            {
                return false;
            }

            foreach (var template in BuildDefaultTemplates())
            {
                template.Id = document.IdSequence.NextPokeTemplateId++;
                PrepareTemplateForSave(template, document.IdSequence.NextPokeCellId);
                document.IdSequence.NextPokeCellId = Math.Max(document.IdSequence.NextPokeCellId, template.Cells.Select(c => c.Id).DefaultIfEmpty(document.IdSequence.NextPokeCellId - 1).Max() + 1);
                document.PokeTemplates.Add(template);
            }

            return true;
        }

        private static void PrepareTemplateForSave(PokeTemplate template, int nextCellId)
        {
            template.AnimationDurationMs = Math.Clamp(template.AnimationDurationMs, 300, 10000);
            template.ResultDisplayDurationSeconds = Math.Clamp(template.ResultDisplayDurationSeconds, 1, 300);
            var orderedCells = template.Cells.OrderBy(c => c.Index).ToList();
            for (var index = 0; index < orderedCells.Count; index++)
            {
                var cell = orderedCells[index];
                cell.Index = index;
                if (cell.Id <= 0)
                {
                    cell.Id = nextCellId++;
                }

                cell.TemplateId = template.Id;
                cell.Template = null!;
            }

            template.Cells = orderedCells;
        }

        private static string BuildDuplicateName(string sourceName, IEnumerable<string> existingNames)
        {
            var baseName = string.IsNullOrWhiteSpace(sourceName)
                ? "複製模板"
                : $"{sourceName} - 複製";

            if (!existingNames.Contains(baseName))
            {
                return baseName;
            }

            var suffix = 2;
            var candidate = $"{baseName} {suffix}";
            while (existingNames.Contains(candidate))
            {
                suffix++;
                candidate = $"{baseName} {suffix}";
            }

            return candidate;
        }

        private async Task RecordAuditAsync(EasyLotteryDomain.Models.Config.EasyLotteryConfigDocument document, string action, string targetName, string details, string? beforeJson = null, string? afterJson = null)
        {
            if (_auditService != null)
            {
                await _auditService.RecordAsync("Template", action, targetName, details, changedBy: document.SystemSettings.Audit.ActorName, beforeJson: beforeJson, afterJson: afterJson);
            }
        }

        public static List<PokeTemplate> BuildDefaultTemplates()
        {
            return new List<PokeTemplate>
            {
                BuildClassicTemplate(),
                BuildCuteTemplate(),
                BuildStreamerTemplate()
            };
        }

        private static PokeTemplate BuildClassicTemplate()
        {
            var template = new PokeTemplate
            {
                Name = "經典模板",
                Description = "9 格、文字搭配破洞圖示",
                GridRows = 3,
                GridColumns = 3,
                Mode = PokeMode.Random,
                Animation = PokeAnimation.Burst,
                IsBuiltIn = true,
                PublicationStatus = TemplatePublicationStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            template.Cells = CreateCells(template.GridRows, template.GridColumns, "#e0e0e0");
            return template;
        }

        private static PokeTemplate BuildCuteTemplate()
        {
            var template = new PokeTemplate
            {
                Name = "可愛模板",
                Description = "卡通格子 + 柔光效果",
                GridRows = 4,
                GridColumns = 4,
                Mode = PokeMode.Random,
                Animation = PokeAnimation.Bounce,
                IsBuiltIn = true,
                PublicationStatus = TemplatePublicationStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            template.Cells = CreateCells(template.GridRows, template.GridColumns, "#ffe4e1");
            return template;
        }

        private static PokeTemplate BuildStreamerTemplate()
        {
            var template = new PokeTemplate
            {
                Name = "實況特效模板",
                Description = "亮光、強特效，適合直播使用",
                GridRows = 5,
                GridColumns = 5,
                Mode = PokeMode.Random,
                Animation = PokeAnimation.Flash,
                IsBuiltIn = true,
                PublicationStatus = TemplatePublicationStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            template.Cells = CreateCells(template.GridRows, template.GridColumns, "#ffd700");
            return template;
        }

        private static List<PokeCell> CreateCells(int rows, int cols, string revealedColor)
        {
            var cells = new List<PokeCell>();
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    cells.Add(new PokeCell
                    {
                        Index = r * cols + c,
                        Title = $"格子 {r * cols + c + 1}",
                        SubTitle = "",
                        RevealedColor = revealedColor,
                    });
                }
            }
            return cells;
        }
    }
}
