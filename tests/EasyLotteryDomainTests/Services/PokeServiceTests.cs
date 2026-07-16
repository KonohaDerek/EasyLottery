using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;
using EasyLotteryDomainTests.Helpers;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class PokeServiceTests
    {
        // ── BuildDefaultTemplates ─────────────────────────────────────────────

        [TestMethod]
        public void BuildDefaultTemplates_ReturnsThreeTemplates()
        {
            var templates = PokeService.BuildDefaultTemplates();
            Assert.AreEqual(3, templates.Count);
        }

        [TestMethod]
        public void BuildDefaultTemplates_ClassicTemplateHasNineCells()
        {
            var templates = PokeService.BuildDefaultTemplates();
            var classic = templates[0];
            Assert.AreEqual(3, classic.GridRows);
            Assert.AreEqual(3, classic.GridColumns);
            Assert.AreEqual(9, classic.Cells.Count);
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task CreateTemplate_PersistsTemplate()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());
            var template = new PokeTemplate { Name = "Test", GridRows = 2, GridColumns = 2 };
            template.Cells = Enumerable.Range(0, 4)
                .Select(i => new PokeCell { Index = i, Title = $"Cell {i}" })
                .ToList();

            var created = await svc.CreateTemplateAsync(template);

            Assert.IsTrue(created.Id > 0);
            Assert.AreEqual("Test", created.Name);
        }

        [TestMethod]
        public async Task ListTemplates_ReturnsAllTemplates()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            await svc.CreateTemplateAsync(new PokeTemplate { Name = "A" });
            await svc.CreateTemplateAsync(new PokeTemplate { Name = "B" });

            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(2, list.Count(t => !t.IsBuiltIn));
        }

        [TestMethod]
        public async Task LoadTemplate_ReturnsCells()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate { Name = "T", GridRows = 1, GridColumns = 3 };
            template.Cells = Enumerable.Range(0, 3).Select(i => new PokeCell { Index = i }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            var loaded = await svc.LoadTemplateAsync(created.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(3, loaded.Cells.Count);
        }

        [TestMethod]
        public async Task DeleteTemplate_RemovesTemplate()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var created = await svc.CreateTemplateAsync(new PokeTemplate { Name = "Del" });
            await svc.DeleteTemplateAsync(created.Id);

            var loaded = await svc.LoadTemplateAsync(created.Id);
            Assert.IsNull(loaded);
        }

        [TestMethod]
        public async Task DuplicateTemplate_CreatesEditableCopy()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate
            {
                Name = "Original",
                Description = "Base template",
                GridRows = 2,
                GridColumns = 3,
                Mode = PokeMode.Manual,
                AllowRePoking = true,
                MaxPokeCount = 4,
                BackgroundImageUrl = "/bg.png",
                FontFamily = "Noto Sans TC",
                CongratulationMessage = "恭喜中獎",
                OverlayWidth = 1280,
                OverlayHeight = 720,
                Animation = PokeAnimation.Flash,
                PokeSoundUrl = "/poke.mp3",
                OpenSoundUrl = "/open.mp3",
                Cells = new List<PokeCell>
                {
                    new PokeCell { Index = 0, Title = "A", SubTitle = "One", ImageUrl = "/a.png", RevealedImageUrl = "/ra.png", RevealedColor = "#111111", IsRevealed = true, RevealedAt = DateTime.UtcNow },
                    new PokeCell { Index = 1, Title = "B", SubTitle = "Two", ImageUrl = "/b.png", RevealedImageUrl = "/rb.png", RevealedColor = "#222222" }
                }
            };
            var created = await svc.CreateTemplateAsync(template);

            var duplicate = await svc.DuplicateTemplateAsync(created.Id);
            var loaded = await svc.LoadTemplateAsync(duplicate.Id);

            Assert.IsNotNull(loaded);
            Assert.AreNotEqual(created.Id, duplicate.Id);
            Assert.AreEqual("Original - 複製", duplicate.Name);
            Assert.IsFalse(duplicate.IsBuiltIn);
            Assert.AreEqual(TemplatePublicationStatus.Draft, duplicate.PublicationStatus);
            Assert.AreEqual(template.Description, duplicate.Description);
            Assert.AreEqual(template.GridRows, duplicate.GridRows);
            Assert.AreEqual(template.GridColumns, duplicate.GridColumns);
            Assert.AreEqual(template.Mode, duplicate.Mode);
            Assert.AreEqual(template.AllowRePoking, duplicate.AllowRePoking);
            Assert.AreEqual(template.MaxPokeCount, duplicate.MaxPokeCount);
            Assert.AreEqual(template.BackgroundImageUrl, duplicate.BackgroundImageUrl);
            Assert.AreEqual(template.FontFamily, duplicate.FontFamily);
            Assert.AreEqual(template.CongratulationMessage, duplicate.CongratulationMessage);
            Assert.AreEqual(template.OverlayWidth, duplicate.OverlayWidth);
            Assert.AreEqual(template.OverlayHeight, duplicate.OverlayHeight);
            Assert.AreEqual(template.Animation, duplicate.Animation);
            Assert.AreEqual(template.PokeSoundUrl, duplicate.PokeSoundUrl);
            Assert.AreEqual(template.OpenSoundUrl, duplicate.OpenSoundUrl);
            Assert.AreEqual(2, duplicate.Cells.Count);
            Assert.IsTrue(duplicate.Cells.All(cell => !cell.IsRevealed));
            Assert.IsTrue(duplicate.Cells.All(cell => cell.RevealedAt == null));
            Assert.AreEqual("A", duplicate.Cells[0].Title);
            Assert.AreEqual("B", duplicate.Cells[1].Title);
        }

        [TestMethod]
        public async Task SetPublicationStatus_UpdatesTemplateStatus()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var created = await svc.CreateTemplateAsync(new PokeTemplate { Name = "Status" });

            var updated = await svc.SetPublicationStatusAsync(created.Id, TemplatePublicationStatus.Draft);

            Assert.AreEqual(TemplatePublicationStatus.Draft, updated.PublicationStatus);
            var loaded = await svc.LoadTemplateAsync(created.Id);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(TemplatePublicationStatus.Draft, loaded.PublicationStatus);
        }

        // ── Poke Logic ────────────────────────────────────────────────────────

        [TestMethod]
        public async Task PokeRandomCell_RevealsOneCell()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate { Name = "Poke", GridRows = 2, GridColumns = 2 };
            template.Cells = Enumerable.Range(0, 4).Select(i => new PokeCell { Index = i }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            var poked = await svc.PokeRandomCellAsync(created.Id);

            Assert.IsNotNull(poked);
            Assert.IsTrue(poked.IsRevealed);
        }

        [TestMethod]
        public async Task PokeRandomCell_ReturnsNull_WhenAllCellsRevealed()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate { Name = "Full", GridRows = 1, GridColumns = 1 };
            template.Cells = new List<PokeCell> { new PokeCell { Index = 0 } };
            var created = await svc.CreateTemplateAsync(template);

            await svc.PokeRandomCellAsync(created.Id); // reveal the only cell
            var result = await svc.PokeRandomCellAsync(created.Id); // should be null

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task PokeCellByIndex_RevealsSpecificCell()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate { Name = "Manual", GridRows = 1, GridColumns = 3 };
            template.Cells = Enumerable.Range(0, 3).Select(i => new PokeCell { Index = i }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            var poked = await svc.PokeCellByIndexAsync(created.Id, 1);

            Assert.IsNotNull(poked);
            Assert.AreEqual(1, poked.Index);
            Assert.IsTrue(poked.IsRevealed);
        }

        [TestMethod]
        public async Task PokeCellByIndex_ReturnsNull_WhenAlreadyRevealedAndNoRePoking()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate { Name = "NoRepoke", AllowRePoking = false };
            template.Cells = new List<PokeCell> { new PokeCell { Index = 0 } };
            var created = await svc.CreateTemplateAsync(template);

            await svc.PokeCellByIndexAsync(created.Id, 0); // first poke
            var result = await svc.PokeCellByIndexAsync(created.Id, 0); // second poke

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetRevealState_ReturnsAllCells()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate { Name = "State", GridRows = 2, GridColumns = 2 };
            template.Cells = Enumerable.Range(0, 4).Select(i => new PokeCell { Index = i }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            var cells = await svc.GetRevealStateAsync(created.Id);

            Assert.AreEqual(4, cells.Count);
        }

        // ── Reset ─────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task ResetTemplate_ClearsRevealedState()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate { Name = "Reset", GridRows = 1, GridColumns = 3 };
            template.Cells = Enumerable.Range(0, 3).Select(i => new PokeCell { Index = i }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            // Poke all cells
            for (int i = 0; i < 3; i++)
                await svc.PokeCellByIndexAsync(created.Id, i);

            await svc.ResetTemplateAsync(created.Id);

            var cells = await svc.GetRevealStateAsync(created.Id);
            Assert.IsTrue(cells.All(c => !c.IsRevealed));
            Assert.IsTrue(cells.All(c => c.RevealedAt == null));
        }

        // ── Export / Import ───────────────────────────────────────────────────

        [TestMethod]
        public async Task ExportImport_RoundTripsTemplate()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            var template = new PokeTemplate { Name = "Export", GridRows = 2, GridColumns = 2 };
            template.Cells = Enumerable.Range(0, 4).Select(i => new PokeCell { Index = i, Title = $"C{i}" }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            var json = await svc.ExportTemplateAsync(created.Id);
            var imported = await svc.ImportTemplateAsync(json);

            Assert.AreNotEqual(created.Id, imported.Id); // new record
            Assert.AreEqual("Export", imported.Name);
            Assert.AreEqual(4, imported.Cells.Count);
        }

        // ── SeedDefaultTemplates ──────────────────────────────────────────────

        [TestMethod]
        public async Task SeedDefaultTemplates_CreatesThreeBuiltInTemplates()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            await svc.SeedDefaultTemplatesAsync();
            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(3, list.Count);
            Assert.IsTrue(list.All(t => t.IsBuiltIn));
        }

        [TestMethod]
        public async Task SeedDefaultTemplates_DoesNotDuplicateWhenCalledTwice()
        {
            var svc = new PokeService(new InMemoryEasyLotteryConfigStore());

            await svc.SeedDefaultTemplatesAsync();
            await svc.SeedDefaultTemplatesAsync();
            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(3, list.Count);
        }
    }
}
