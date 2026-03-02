using EasyLotteryDomain.Database;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class PokeServiceTests
    {
        private static IDbContextFactory<EasyLotteryContext> CreateFactory(string dbName)
        {
            var options = new DbContextOptionsBuilder<EasyLotteryContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestDbContextFactory(options);
        }

        // Helper factory that bypasses the migration logic in the real constructor
        private sealed class TestDbContextFactory : IDbContextFactory<EasyLotteryContext>
        {
            private readonly DbContextOptions<EasyLotteryContext> _options;
            public TestDbContextFactory(DbContextOptions<EasyLotteryContext> options) => _options = options;
            public EasyLotteryContext CreateDbContext() => new TestEasyLotteryContext(_options);
            public Task<EasyLotteryContext> CreateDbContextAsync(CancellationToken _) =>
                Task.FromResult<EasyLotteryContext>(new TestEasyLotteryContext(_options));
        }

        // Subclass that skips the Migrate() call (not needed for InMemory)
        private sealed class TestEasyLotteryContext : EasyLotteryContext
        {
            public TestEasyLotteryContext(DbContextOptions<EasyLotteryContext> options)
                : base(options, skipMigration: true) { }
        }

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
            var svc = new PokeService(CreateFactory(nameof(CreateTemplate_PersistsTemplate)));
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
            var factory = CreateFactory(nameof(ListTemplates_ReturnsAllTemplates));
            var svc = new PokeService(factory);

            await svc.CreateTemplateAsync(new PokeTemplate { Name = "A" });
            await svc.CreateTemplateAsync(new PokeTemplate { Name = "B" });

            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public async Task LoadTemplate_ReturnsCells()
        {
            var factory = CreateFactory(nameof(LoadTemplate_ReturnsCells));
            var svc = new PokeService(factory);

            var template = new PokeTemplate { Name = "T", GridRows = 1, GridColumns = 3 };
            template.Cells = Enumerable.Range(0, 3).Select(i => new PokeCell { Index = i }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            var loaded = await svc.LoadTemplateAsync(created.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(3, loaded.Cells.Count);
        }

        [TestMethod]
        public async Task DeleteTemplate_RemovesFromDatabase()
        {
            var factory = CreateFactory(nameof(DeleteTemplate_RemovesFromDatabase));
            var svc = new PokeService(factory);

            var created = await svc.CreateTemplateAsync(new PokeTemplate { Name = "Del" });
            await svc.DeleteTemplateAsync(created.Id);

            var loaded = await svc.LoadTemplateAsync(created.Id);
            Assert.IsNull(loaded);
        }

        // ── Poke Logic ────────────────────────────────────────────────────────

        [TestMethod]
        public async Task PokeRandomCell_RevealsOneCell()
        {
            var factory = CreateFactory(nameof(PokeRandomCell_RevealsOneCell));
            var svc = new PokeService(factory);

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
            var factory = CreateFactory(nameof(PokeRandomCell_ReturnsNull_WhenAllCellsRevealed));
            var svc = new PokeService(factory);

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
            var factory = CreateFactory(nameof(PokeCellByIndex_RevealsSpecificCell));
            var svc = new PokeService(factory);

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
            var factory = CreateFactory(nameof(PokeCellByIndex_ReturnsNull_WhenAlreadyRevealedAndNoRePoking));
            var svc = new PokeService(factory);

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
            var factory = CreateFactory(nameof(GetRevealState_ReturnsAllCells));
            var svc = new PokeService(factory);

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
            var factory = CreateFactory(nameof(ResetTemplate_ClearsRevealedState));
            var svc = new PokeService(factory);

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
            var factory = CreateFactory(nameof(ExportImport_RoundTripsTemplate));
            var svc = new PokeService(factory);

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
            var factory = CreateFactory(nameof(SeedDefaultTemplates_CreatesThreeBuiltInTemplates));
            var svc = new PokeService(factory);

            await svc.SeedDefaultTemplatesAsync();
            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(3, list.Count);
            Assert.IsTrue(list.All(t => t.IsBuiltIn));
        }

        [TestMethod]
        public async Task SeedDefaultTemplates_DoesNotDuplicateWhenCalledTwice()
        {
            var factory = CreateFactory(nameof(SeedDefaultTemplates_DoesNotDuplicateWhenCalledTwice));
            var svc = new PokeService(factory);

            await svc.SeedDefaultTemplatesAsync();
            await svc.SeedDefaultTemplatesAsync();
            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(3, list.Count);
        }
    }
}
