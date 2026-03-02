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
    public class PokeService
    {
        private readonly IDbContextFactory<EasyLotteryContext> _contextFactory;

        private static readonly Random _random = Random.Shared;

        public PokeService(IDbContextFactory<EasyLotteryContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        public async Task<PokeTemplate> CreateTemplateAsync(PokeTemplate template)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            template.CreatedAt = DateTime.UtcNow;
            template.UpdatedAt = DateTime.UtcNow;
            ctx.PokeTemplates.Add(template);
            await ctx.SaveChangesAsync();
            return template;
        }

        public async Task UpdateTemplateAsync(PokeTemplate template)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            template.UpdatedAt = DateTime.UtcNow;

            var existing = await ctx.PokeTemplates
                .Include(t => t.Cells)
                .FirstOrDefaultAsync(t => t.Id == template.Id)
                ?? throw new InvalidOperationException($"Template {template.Id} not found.");

            ctx.Entry(existing).CurrentValues.SetValues(template);

            // Sync cells: remove deleted, update existing, add new
            var incomingIds = template.Cells.Where(c => c.Id != 0).Select(c => c.Id).ToHashSet();
            var toRemove = existing.Cells.Where(c => !incomingIds.Contains(c.Id)).ToList();
            ctx.PokeCells.RemoveRange(toRemove);

            foreach (var incomingCell in template.Cells)
            {
                var existingCell = existing.Cells.FirstOrDefault(c => c.Id == incomingCell.Id);
                if (existingCell != null)
                {
                    ctx.Entry(existingCell).CurrentValues.SetValues(incomingCell);
                }
                else
                {
                    incomingCell.TemplateId = template.Id;
                    ctx.PokeCells.Add(incomingCell);
                }
            }

            await ctx.SaveChangesAsync();
        }

        public async Task DeleteTemplateAsync(int id)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            var template = await ctx.PokeTemplates.FindAsync(id)
                ?? throw new InvalidOperationException($"Template {id} not found.");
            ctx.PokeTemplates.Remove(template);
            await ctx.SaveChangesAsync();
        }

        public async Task<List<PokeTemplate>> ListTemplatesAsync()
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            return await ctx.PokeTemplates
                .OrderByDescending(t => t.UpdatedAt)
                .ToListAsync();
        }

        public async Task<PokeTemplate?> LoadTemplateAsync(int id)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            return await ctx.PokeTemplates
                .Include(t => t.Cells.OrderBy(c => c.Index))
                .FirstOrDefaultAsync(t => t.Id == id);
        }

        // ── Poke Logic ────────────────────────────────────────────────────────

        /// <summary>Randomly poke one unrevealed cell. Returns null if all cells are already revealed.</summary>
        public async Task<PokeCell?> PokeRandomCellAsync(int templateId)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            var template = await ctx.PokeTemplates
                .Include(t => t.Cells)
                .FirstOrDefaultAsync(t => t.Id == templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            if (!CanPoke(template)) return null;

            var unrevealed = template.Cells.Where(c => !c.IsRevealed).ToList();
            if (unrevealed.Count == 0) return null;

            var cell = unrevealed[_random.Next(unrevealed.Count)];
            cell.IsRevealed = true;
            cell.RevealedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return cell;
        }

        /// <summary>Manually poke a specific cell by its index.</summary>
        public async Task<PokeCell?> PokeCellByIndexAsync(int templateId, int index)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            var template = await ctx.PokeTemplates
                .Include(t => t.Cells)
                .FirstOrDefaultAsync(t => t.Id == templateId)
                ?? throw new InvalidOperationException($"Template {templateId} not found.");

            var cell = template.Cells.FirstOrDefault(c => c.Index == index)
                ?? throw new InvalidOperationException($"Cell at index {index} not found.");

            if (cell.IsRevealed && !template.AllowRePoking) return null;
            if (!CanPoke(template)) return null;

            cell.IsRevealed = true;
            cell.RevealedAt = DateTime.UtcNow;
            await ctx.SaveChangesAsync();
            return cell;
        }

        /// <summary>Returns the current reveal state for all cells in the template.</summary>
        public async Task<List<PokeCell>> GetRevealStateAsync(int templateId)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            return await ctx.PokeCells
                .Where(c => c.TemplateId == templateId)
                .OrderBy(c => c.Index)
                .ToListAsync();
        }

        // ── Reset ─────────────────────────────────────────────────────────────

        public async Task ResetTemplateAsync(int templateId)
        {
            using var ctx = await _contextFactory.CreateDbContextAsync();
            var cells = await ctx.PokeCells
                .Where(c => c.TemplateId == templateId)
                .ToListAsync();
            foreach (var cell in cells)
            {
                cell.IsRevealed = false;
                cell.RevealedAt = null;
            }
            await ctx.SaveChangesAsync();
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
            template.IsBuiltIn = false;
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
            using var ctx = await _contextFactory.CreateDbContextAsync();
            if (await ctx.PokeTemplates.AnyAsync(t => t.IsBuiltIn)) return;

            var defaults = BuildDefaultTemplates();
            ctx.PokeTemplates.AddRange(defaults);
            await ctx.SaveChangesAsync();
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static bool CanPoke(PokeTemplate template)
        {
            if (template.MaxPokeCount <= 0) return true;
            var revealed = template.Cells.Count(c => c.IsRevealed);
            return revealed < template.MaxPokeCount;
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
