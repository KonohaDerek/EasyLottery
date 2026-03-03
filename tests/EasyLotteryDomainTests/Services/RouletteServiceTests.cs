using EasyLotteryDomain.Database;
using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class RouletteServiceTests
    {
        private static IDbContextFactory<EasyLotteryContext> CreateFactory(string dbName)
        {
            var options = new DbContextOptionsBuilder<EasyLotteryContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestDbContextFactory(options);
        }

        private sealed class TestDbContextFactory : IDbContextFactory<EasyLotteryContext>
        {
            private readonly DbContextOptions<EasyLotteryContext> _options;
            public TestDbContextFactory(DbContextOptions<EasyLotteryContext> options) => _options = options;
            public EasyLotteryContext CreateDbContext() => new TestEasyLotteryContext(_options);
            public Task<EasyLotteryContext> CreateDbContextAsync(CancellationToken _) =>
                Task.FromResult<EasyLotteryContext>(new TestEasyLotteryContext(_options));
        }

        private sealed class TestEasyLotteryContext : EasyLotteryContext
        {
            public TestEasyLotteryContext(DbContextOptions<EasyLotteryContext> options)
                : base(options, skipMigration: true) { }
        }

        // ── BuildDefaultTemplates ─────────────────────────────────────────────

        [TestMethod]
        public void BuildDefaultTemplates_ReturnsThreeTemplates()
        {
            var templates = RouletteService.BuildDefaultTemplates();
            Assert.AreEqual(3, templates.Count);
        }

        [TestMethod]
        public void BuildDefaultTemplates_CasinoHasEightSegments()
        {
            var templates = RouletteService.BuildDefaultTemplates();
            var casino = templates[0];
            Assert.AreEqual(8, casino.SegmentCount);
            Assert.AreEqual(8, casino.Segments.Count);
        }

        [TestMethod]
        public void BuildDefaultTemplates_CandyHasTwelveSegments()
        {
            var templates = RouletteService.BuildDefaultTemplates();
            var candy = templates[1];
            Assert.AreEqual(12, candy.SegmentCount);
            Assert.AreEqual(12, candy.Segments.Count);
        }

        [TestMethod]
        public void BuildDefaultTemplates_StreamerHasSixSegments()
        {
            var templates = RouletteService.BuildDefaultTemplates();
            var streamer = templates[2];
            Assert.AreEqual(6, streamer.SegmentCount);
            Assert.AreEqual(6, streamer.Segments.Count);
        }

        // ── CRUD ──────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task CreateTemplate_PersistsTemplate()
        {
            var svc = new RouletteService(CreateFactory(nameof(CreateTemplate_PersistsTemplate)));
            var template = new RouletteTemplate { Name = "Test", SegmentCount = 6 };
            template.Segments = Enumerable.Range(0, 6)
                .Select(i => new RouletteSegment { Index = i, Title = $"Seg {i}" })
                .ToList();

            var created = await svc.CreateTemplateAsync(template);

            Assert.IsTrue(created.Id > 0);
            Assert.AreEqual("Test", created.Name);
        }

        [TestMethod]
        public async Task ListTemplates_ReturnsAllTemplates()
        {
            var factory = CreateFactory(nameof(ListTemplates_ReturnsAllTemplates));
            var svc = new RouletteService(factory);

            await svc.CreateTemplateAsync(new RouletteTemplate { Name = "A" });
            await svc.CreateTemplateAsync(new RouletteTemplate { Name = "B" });

            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(2, list.Count);
        }

        [TestMethod]
        public async Task LoadTemplate_ReturnsSegments()
        {
            var factory = CreateFactory(nameof(LoadTemplate_ReturnsSegments));
            var svc = new RouletteService(factory);

            var template = new RouletteTemplate { Name = "T", SegmentCount = 8 };
            template.Segments = Enumerable.Range(0, 8).Select(i => new RouletteSegment { Index = i }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            var loaded = await svc.LoadTemplateAsync(created.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(8, loaded.Segments.Count);
        }

        [TestMethod]
        public async Task DeleteTemplate_RemovesFromDatabase()
        {
            var factory = CreateFactory(nameof(DeleteTemplate_RemovesFromDatabase));
            var svc = new RouletteService(factory);

            var created = await svc.CreateTemplateAsync(new RouletteTemplate { Name = "Del" });
            await svc.DeleteTemplateAsync(created.Id);

            var loaded = await svc.LoadTemplateAsync(created.Id);
            Assert.IsNull(loaded);
        }

        // ── Spin Algorithm ────────────────────────────────────────────────────

        [TestMethod]
        public void Spin_ReturnsValidSegmentIndex()
        {
            var template = new RouletteTemplate { Name = "Spin", SegmentCount = 8 };
            template.Segments = Enumerable.Range(0, 8)
                .Select(i => new RouletteSegment { Index = i, Title = $"Seg {i}", Probability = 0 })
                .ToList();

            var result = RouletteService.Spin(template);

            Assert.IsTrue(result.SegmentIndex >= 0 && result.SegmentIndex < 8);
            Assert.IsTrue(result.TotalRotationDeg > 360);
        }

        [TestMethod]
        public void Spin_ForceIndex_ReturnsCorrectSegment()
        {
            var template = new RouletteTemplate { Name = "Force", SegmentCount = 6 };
            template.Segments = Enumerable.Range(0, 6)
                .Select(i => new RouletteSegment { Index = i, Title = $"Seg {i}" })
                .ToList();

            var result = RouletteService.Spin(template, forceIndex: 3);

            Assert.AreEqual(3, result.SegmentIndex);
            Assert.AreEqual("Seg 3", result.SegmentTitle);
        }

        [TestMethod]
        public void Spin_WithWeightedProbability_PicksFromNonZeroSegments()
        {
            var template = new RouletteTemplate { Name = "Weighted", SegmentCount = 4 };
            template.Segments = new List<RouletteSegment>
            {
                new RouletteSegment { Index = 0, Title = "A", Probability = 0 },
                new RouletteSegment { Index = 1, Title = "B", Probability = 100 },
                new RouletteSegment { Index = 2, Title = "C", Probability = 0 },
                new RouletteSegment { Index = 3, Title = "D", Probability = 0 },
            };

            // With 100% on index 1, it should always land there
            for (int i = 0; i < 20; i++)
            {
                var result = RouletteService.Spin(template);
                Assert.AreEqual(1, result.SegmentIndex);
            }
        }

        [TestMethod]
        public void Spin_MinimumRotation_IsAtLeastFiveRevolutions()
        {
            var template = new RouletteTemplate { Name = "MinRot", SegmentCount = 8 };
            template.Segments = Enumerable.Range(0, 8)
                .Select(i => new RouletteSegment { Index = i })
                .ToList();

            var result = RouletteService.Spin(template);

            Assert.IsTrue(result.TotalRotationDeg >= 360 * 5);
        }

        [TestMethod]
        public void Spin_ThrowsOnEmptySegments()
        {
            var template = new RouletteTemplate { Name = "Empty", SegmentCount = 0 };

            Assert.ThrowsException<InvalidOperationException>(() => RouletteService.Spin(template));
        }

        // ── Export / Import ───────────────────────────────────────────────────

        [TestMethod]
        public async Task ExportImport_RoundTripsTemplate()
        {
            var factory = CreateFactory(nameof(ExportImport_RoundTripsTemplate));
            var svc = new RouletteService(factory);

            var template = new RouletteTemplate { Name = "Export", SegmentCount = 6 };
            template.Segments = Enumerable.Range(0, 6)
                .Select(i => new RouletteSegment { Index = i, Title = $"S{i}" })
                .ToList();
            var created = await svc.CreateTemplateAsync(template);

            var json = await svc.ExportTemplateAsync(created.Id);
            var imported = await svc.ImportTemplateAsync(json);

            Assert.AreNotEqual(created.Id, imported.Id);
            Assert.AreEqual("Export", imported.Name);
            Assert.AreEqual(6, imported.Segments.Count);
        }

        // ── SeedDefaultTemplates ──────────────────────────────────────────────

        [TestMethod]
        public async Task SeedDefaultTemplates_CreatesThreeBuiltInTemplates()
        {
            var factory = CreateFactory(nameof(SeedDefaultTemplates_CreatesThreeBuiltInTemplates));
            var svc = new RouletteService(factory);

            await svc.SeedDefaultTemplatesAsync();
            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(3, list.Count);
            Assert.IsTrue(list.All(t => t.IsBuiltIn));
        }

        [TestMethod]
        public async Task SeedDefaultTemplates_DoesNotDuplicateWhenCalledTwice()
        {
            var factory = CreateFactory(nameof(SeedDefaultTemplates_DoesNotDuplicateWhenCalledTwice));
            var svc = new RouletteService(factory);

            await svc.SeedDefaultTemplatesAsync();
            await svc.SeedDefaultTemplatesAsync();
            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(3, list.Count);
        }
    }
}
