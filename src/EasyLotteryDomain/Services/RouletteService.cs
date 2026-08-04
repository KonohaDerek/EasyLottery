using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EasyLotteryDomain.Models.Entities;

namespace EasyLotteryDomain.Services
{
    public class RouletteService
    {
        private readonly IEasyLotteryConfigStore _configStore;
        private readonly EasyLotteryAuditService? _auditService;
        private static readonly Random _random = Random.Shared;

        private const double MinRevolutions = 5;
        private const double SegmentOffsetFactor = 0.6;

        public RouletteService(IEasyLotteryConfigStore configStore, EasyLotteryAuditService? auditService = null)
        {
            _configStore = configStore;
            _auditService = auditService;
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        public async Task<RouletteTemplate> CreateTemplateAsync(RouletteTemplate template)
        {
            var document = await _configStore.LoadAsync();
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            template.Id = document.IdSequence.NextRouletteTemplateId++;
            template.PublicId = Guid.NewGuid();
            PrepareTemplateForSave(template, document.IdSequence.NextRouletteSegmentId);
            document.IdSequence.NextRouletteSegmentId = Math.Max(document.IdSequence.NextRouletteSegmentId, template.Segments.Select(s => s.Id).DefaultIfEmpty(document.IdSequence.NextRouletteSegmentId - 1).Max() + 1);
            document.RouletteTemplates.Add(template);
            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, "建立轉盤模板", template.Name, $"模板 ID {template.Id}");
            return template;
        }

        public async Task UpdateTemplateAsync(RouletteTemplate template)
        {
            var document = await _configStore.LoadAsync();
            template.UpdatedAt = DateTime.UtcNow;

            var existing = document.RouletteTemplates
                .FirstOrDefault(t => t.Id == template.Id)
                ?? throw new InvalidOperationException($"Template {template.Id} not found.");

            template.CreatedAt = existing.CreatedAt;
            template.PublicId = existing.PublicId != Guid.Empty
                ? existing.PublicId
                : template.PublicId != Guid.Empty ? template.PublicId : Guid.NewGuid();
            PrepareTemplateForSave(template, document.IdSequence.NextRouletteSegmentId);
            document.IdSequence.NextRouletteSegmentId = Math.Max(document.IdSequence.NextRouletteSegmentId, template.Segments.Select(s => s.Id).DefaultIfEmpty(document.IdSequence.NextRouletteSegmentId - 1).Max() + 1);

            var existingIndex = document.RouletteTemplates.FindIndex(t => t.Id == template.Id);
            document.RouletteTemplates[existingIndex] = template;
            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, "更新轉盤模板", template.Name, $"模板 ID {template.Id}");
        }

        public async Task DeleteTemplateAsync(int id)
        {
            var document = await _configStore.LoadAsync();
            var template = document.RouletteTemplates.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"Template {id} not found.");
            document.RouletteTemplates.Remove(template);
            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, "刪除轉盤模板", template.Name, $"模板 ID {template.Id}");
        }

        public async Task<RouletteTemplate> DuplicateTemplateAsync(int id)
        {
            var document = await _configStore.LoadAsync();
            var source = document.RouletteTemplates.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"Template {id} not found.");

            var duplicate = new RouletteTemplate
            {
                Name = BuildDuplicateName(source.Name, document.RouletteTemplates.Select(t => t.Name)),
                Description = source.Description,
                SegmentCount = source.SegmentCount,
                SpinDurationSec = source.SpinDurationSec,
                ResultDisplayDurationSeconds = source.ResultDisplayDurationSeconds,
                EasingFunction = source.EasingFunction,
                InitialAngleDeg = source.InitialAngleDeg,
                CenterImageUrl = source.CenterImageUrl,
                BackgroundImageUrl = source.BackgroundImageUrl,
                PointerImageUrl = source.PointerImageUrl,
                SpinSoundUrl = source.SpinSoundUrl,
                WinSoundUrl = source.WinSoundUrl,
                IsBuiltIn = false,
                PublicationStatus = TemplatePublicationStatus.Draft,
                Segments = source.Segments
                    .OrderBy(segment => segment.Index)
                    .Select(segment => new RouletteSegment
                    {
                        Index = segment.Index,
                        Title = segment.Title,
                        ImageUrl = segment.ImageUrl,
                        Color = segment.Color,
                        Probability = segment.Probability
                    })
                    .ToList()
            };

            var created = await CreateTemplateAsync(duplicate);
            await RecordAuditAsync(document, "複製轉盤模板", created.Name, $"來源模板 ID {source.Id}");
            return created;
        }

        public async Task<List<RouletteTemplate>> ListTemplatesAsync()
        {
            var document = await LoadDocumentAsync(ensureBuiltIns: true);
            return document.RouletteTemplates
                .OrderBy(t => t.PublicationStatus)
                .ThenByDescending(t => t.UpdatedAt)
                .ToList();
        }

        public async Task<RouletteTemplate?> LoadTemplateAsync(int id)
        {
            var document = await LoadDocumentAsync(ensureBuiltIns: true);
            return document.RouletteTemplates
                .FirstOrDefault(t => t.Id == id);
        }

        public async Task<RouletteTemplate?> LoadTemplateAsync(Guid publicId)
        {
            var document = await LoadDocumentAsync(ensureBuiltIns: true);
            return document.RouletteTemplates.FirstOrDefault(t => t.PublicId == publicId);
        }

        public async Task<RouletteTemplate> SetPublicationStatusAsync(int id, TemplatePublicationStatus status)
        {
            var document = await _configStore.LoadAsync();
            var template = document.RouletteTemplates.FirstOrDefault(t => t.Id == id)
                ?? throw new InvalidOperationException($"Template {id} not found.");

            template.PublicationStatus = status;
            template.UpdatedAt = DateTime.UtcNow;
            await _configStore.SaveAsync(document);
            await RecordAuditAsync(document, status == TemplatePublicationStatus.Published ? "發布轉盤模板" : "轉為轉盤草稿", template.Name, $"模板 ID {template.Id}");
            return template;
        }

        // ── Spin Algorithm ────────────────────────────────────────────────────

        /// <summary>
        /// Performs a weighted random spin and returns the winning segment index
        /// plus the total rotation in degrees for the animation.
        /// </summary>
        public async Task<SpinResult> SpinAsync(int templateId, int? forceIndex = null, double currentRotation = 0)
        {
            var template = await LoadTemplateAsync(templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            return Spin(template, forceIndex, currentRotation);
        }

        /// <summary>
        /// Pure (non-async) spin calculation for easy unit testing.
        /// <param name="currentRotation">The wheel's current cumulative rotation in degrees (used to compute the correct delta).</param>
        /// </summary>
        public static SpinResult Spin(RouletteTemplate template, int? forceIndex = null, double currentRotation = 0)
        {
            var segments = template.Segments.OrderBy(s => s.Index).ToList();
            int count = segments.Count;
            if (count == 0)
                throw new InvalidOperationException("Template has no segments.");

            int winnerIndex;
            if (forceIndex.HasValue)
            {
                winnerIndex = Math.Clamp(forceIndex.Value, 0, count - 1);
            }
            else
            {
                winnerIndex = PickWeighted(segments);
            }

            double segmentAngle = 360.0 / count;

            // Angle of the winning segment's center measured clockwise from top
            double winnerCenterAngle = winnerIndex * segmentAngle + segmentAngle / 2.0;

            // Target absolute angle (mod 360) so the pointer lands on the winner
            double offsetWithinSegment = (_random.NextDouble() - 0.5) * segmentAngle * SegmentOffsetFactor;
            double targetMod = ((360.0 - winnerCenterAngle) + offsetWithinSegment) % 360.0;
            if (targetMod < 0) targetMod += 360.0;

            // Account for the wheel's current rotation to compute the correct delta
            double currentMod = currentRotation % 360.0;
            if (currentMod < 0) currentMod += 360.0;

            double delta = (targetMod - currentMod + 360.0) % 360.0;

            // Add full revolutions so the spin always does at least MinRevolutions turns
            double totalRotation = 360.0 * MinRevolutions + delta;

            return new SpinResult
            {
                SegmentIndex = winnerIndex,
                TotalRotationDeg = totalRotation,
                SegmentTitle = segments[winnerIndex].Title
            };
        }

        private static int PickWeighted(List<RouletteSegment> segments)
        {
            double totalProb = segments.Sum(s => s.Probability);
            if (totalProb <= 0)
            {
                // Equal distribution
                return _random.Next(segments.Count);
            }

            double roll = _random.NextDouble() * totalProb;
            double cumulative = 0;
            for (int i = 0; i < segments.Count; i++)
            {
                cumulative += segments[i].Probability;
                if (roll < cumulative) return i;
            }
            return segments.Count - 1;
        }

        // ── Export / Import ───────────────────────────────────────────────────

        public async Task<string> ExportTemplateAsync(int templateId)
        {
            var template = await LoadTemplateAsync(templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            foreach (var seg in template.Segments)
                seg.Template = null!;

            return JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
        }

        public async Task<RouletteTemplate> ImportTemplateAsync(string json)
        {
            var template = JsonSerializer.Deserialize<RouletteTemplate>(json)
                ?? throw new ArgumentException("Invalid template JSON.");

            template.Id = 0;
            template.PublicId = Guid.Empty;
            template.IsBuiltIn = false;
            template.PublicationStatus = TemplatePublicationStatus.Draft;
            foreach (var seg in template.Segments)
            {
                seg.Id = 0;
                seg.TemplateId = 0;
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
            await RecordAuditAsync(document, "建立內建轉盤模板", "預設模板", "初始化 3 組內建模板");
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
            foreach (var template in document.RouletteTemplates.Where(template => template.PublicId == Guid.Empty))
            {
                template.PublicId = Guid.NewGuid();
                changed = true;
            }

            return changed;
        }

        private static bool EnsureBuiltInTemplates(EasyLotteryDomain.Models.Config.EasyLotteryConfigDocument document)
        {
            if (document.RouletteTemplates.Any(t => t.IsBuiltIn))
            {
                return false;
            }

            foreach (var template in BuildDefaultTemplates())
            {
                template.Id = document.IdSequence.NextRouletteTemplateId++;
                PrepareTemplateForSave(template, document.IdSequence.NextRouletteSegmentId);
                document.IdSequence.NextRouletteSegmentId = Math.Max(document.IdSequence.NextRouletteSegmentId, template.Segments.Select(s => s.Id).DefaultIfEmpty(document.IdSequence.NextRouletteSegmentId - 1).Max() + 1);
                document.RouletteTemplates.Add(template);
            }

            return true;
        }

        private async Task RecordAuditAsync(EasyLotteryDomain.Models.Config.EasyLotteryConfigDocument document, string action, string targetName, string details)
        {
            if (_auditService != null)
            {
                await _auditService.RecordAsync("Template", action, targetName, details, changedBy: document.SystemSettings.Audit.ActorName);
            }
        }

        private static void PrepareTemplateForSave(RouletteTemplate template, int nextSegmentId)
        {
            template.SpinDurationSec = Math.Clamp(template.SpinDurationSec, 1, 30);
            template.ResultDisplayDurationSeconds = Math.Clamp(template.ResultDisplayDurationSeconds, 1, 300);
            var orderedSegments = template.Segments.OrderBy(s => s.Index).ToList();
            for (var index = 0; index < orderedSegments.Count; index++)
            {
                var segment = orderedSegments[index];
                segment.Index = index;
                if (segment.Id <= 0)
                {
                    segment.Id = nextSegmentId++;
                }

                segment.TemplateId = template.Id;
                segment.Template = null!;
            }

            template.Segments = orderedSegments;
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

        public static List<RouletteTemplate> BuildDefaultTemplates()
        {
            return new List<RouletteTemplate>
            {
                BuildCasinoTemplate(),
                BuildCandyTemplate(),
                BuildStreamerPenaltyTemplate()
            };
        }

        private static RouletteTemplate BuildCasinoTemplate()
        {
            var colors = new[] { "#c0392b", "#2c3e50", "#f39c12", "#2c3e50", "#27ae60", "#2c3e50", "#8e44ad", "#2c3e50" };
            var template = new RouletteTemplate
            {
                Name = "Casino 樣式",
                Description = "經典賭場風格 8 格轉盤",
                SegmentCount = 8,
                SpinDurationSec = 5.0,
                EasingFunction = "ease-out-cubic",
                IsBuiltIn = true,
                PublicationStatus = TemplatePublicationStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            template.Segments = CreateSegments(8, colors);
            return template;
        }

        private static RouletteTemplate BuildCandyTemplate()
        {
            var colors = new[] { "#ff6b9d", "#ffd93d", "#6bcb77", "#4d96ff", "#ff6b9d", "#ffd93d", "#6bcb77", "#4d96ff",
                                  "#ff9a3c", "#c77dff", "#ff6b9d", "#ffd93d" };
            var template = new RouletteTemplate
            {
                Name = "可愛糖果轉盤",
                Description = "繽紛可愛風格 12 格轉盤",
                SegmentCount = 12,
                SpinDurationSec = 4.0,
                EasingFunction = "ease-out-cubic",
                IsBuiltIn = true,
                PublicationStatus = TemplatePublicationStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            template.Segments = CreateSegments(12, colors);
            return template;
        }

        private static RouletteTemplate BuildStreamerPenaltyTemplate()
        {
            var colors = new[] { "#e74c3c", "#e67e22", "#f1c40f", "#2ecc71", "#1abc9c", "#3498db" };
            var template = new RouletteTemplate
            {
                Name = "實況懲罰轉盤",
                Description = "適合直播互動的 6 格懲罰轉盤",
                SegmentCount = 6,
                SpinDurationSec = 6.0,
                EasingFunction = "ease-out-cubic",
                IsBuiltIn = true,
                PublicationStatus = TemplatePublicationStatus.Published,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            template.Segments = CreateSegments(6, colors);
            return template;
        }

        private static List<RouletteSegment> CreateSegments(int count, string[] colors)
        {
            var segments = new List<RouletteSegment>();
            for (int i = 0; i < count; i++)
            {
                segments.Add(new RouletteSegment
                {
                    Index = i,
                    Title = $"選項 {i + 1}",
                    Color = colors[i % colors.Length],
                    Probability = 0
                });
            }
            return segments;
        }
    }
}
