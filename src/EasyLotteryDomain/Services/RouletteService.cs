using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EasyLotteryDomain.Database;
using EasyLotteryDomain.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryDomain.Services
{
    public class RouletteService
    {
        private readonly IDbContextFactory<EasyLotteryContext> _contextFactory;
        private static readonly Random _random = Random.Shared;

        private const double MinRevolutions = 5;
        private const double SegmentOffsetFactor = 0.6;

        public RouletteService(IDbContextFactory<EasyLotteryContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        public async Task<RouletteTemplate> CreateTemplateAsync(RouletteTemplate template)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            ctx.RouletteTemplates.Add(template);
            await ctx.SaveChangesAsync();
            return template;
        }

        public async Task UpdateTemplateAsync(RouletteTemplate template)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            template.UpdatedAt = DateTime.UtcNow;

            var existing = await ctx.RouletteTemplates
                .Include(t => t.Segments)
                .FirstOrDefaultAsync(t => t.Id == template.Id)
                ?? throw new InvalidOperationException($"Template {template.Id} not found.");

            ctx.Entry(existing).CurrentValues.SetValues(template);

            // Sync segments: remove deleted, update existing, add new
            var incomingIds = template.Segments.Where(s => s.Id != 0).Select(s => s.Id).ToHashSet();
            var toRemove = existing.Segments.Where(s => !incomingIds.Contains(s.Id)).ToList();
            ctx.RouletteSegments.RemoveRange(toRemove);

            foreach (var incoming in template.Segments)
            {
                var existingSegment = existing.Segments.FirstOrDefault(s => s.Id == incoming.Id);
                if (existingSegment != null)
                {
                    ctx.Entry(existingSegment).CurrentValues.SetValues(incoming);
                }
                else
                {
                    incoming.TemplateId = template.Id;
                    ctx.RouletteSegments.Add(incoming);
                }
            }

            await ctx.SaveChangesAsync();
        }

        public async Task DeleteTemplateAsync(int id)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            var template = await ctx.RouletteTemplates.FindAsync(id)
                ?? throw new InvalidOperationException($"Template {id} not found.");
            ctx.RouletteTemplates.Remove(template);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<RouletteTemplate>> ListTemplatesAsync()
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            return await ctx.RouletteTemplates
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync();
        }

        public async Task<RouletteTemplate?> LoadTemplateAsync(int id)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            return await ctx.RouletteTemplates
                .Include(t => t.Segments.OrderBy(s => s.Index))
                .FirstOrDefaultAsync(t => t.Id == id);
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
            template.IsBuiltIn = false;
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
            using var ctx = await _contextFactory.CreateDbContextAsync();
            if (await ctx.RouletteTemplates.AnyAsync(t => t.IsBuiltIn)) return;

            var defaults = BuildDefaultTemplates();
            ctx.RouletteTemplates.AddRange(defaults);
            await ctx.SaveChangesAsync();
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
