using EasyLotteryDomain.Models.Entities;
using EasyLotteryDomain.Services;
using EasyLotteryDomainTests.Helpers;

namespace EasyLotteryDomainTests.Services
{
    [TestClass]
    public class RouletteServiceTests
    {
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
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());
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
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

            await svc.CreateTemplateAsync(new RouletteTemplate { Name = "A" });
            await svc.CreateTemplateAsync(new RouletteTemplate { Name = "B" });

            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(2, list.Count(t => !t.IsBuiltIn));
        }

        [TestMethod]
        public async Task LoadTemplate_ReturnsSegments()
        {
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

            var template = new RouletteTemplate { Name = "T", SegmentCount = 8 };
            template.Segments = Enumerable.Range(0, 8).Select(i => new RouletteSegment { Index = i }).ToList();
            var created = await svc.CreateTemplateAsync(template);

            var loaded = await svc.LoadTemplateAsync(created.Id);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(8, loaded.Segments.Count);
        }

        [TestMethod]
        public async Task DeleteTemplate_RemovesTemplate()
        {
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

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

        [TestMethod]
        public void Spin_ConsecutiveSpins_PointerAlwaysMatchesWinner()
        {
            // Simulates multiple consecutive spins, verifying the pointer
            // always lands within the winning segment's angular range.
            var template = new RouletteTemplate { Name = "ConsecutiveTest", SegmentCount = 8 };
            template.Segments = Enumerable.Range(0, 8)
                .Select(i => new RouletteSegment { Index = i, Title = $"Seg {i}", Probability = 0 })
                .ToList();

            double currentRotation = 0;
            for (int spin = 0; spin < 50; spin++)
            {
                var result = RouletteService.Spin(template, currentRotation: currentRotation);
                currentRotation += result.TotalRotationDeg;

                // After rotation, the pointer (at top) points at angle (360 - currentRotation % 360) % 360
                double pointerAngle = (360.0 - currentRotation % 360.0) % 360.0;
                if (pointerAngle < 0) pointerAngle += 360.0;

                int expectedCount = template.Segments.Count;
                double segAngle = 360.0 / expectedCount;

                // Determine which segment the pointer actually lands on
                int actualIndex = (int)(pointerAngle / segAngle);
                if (actualIndex >= expectedCount) actualIndex = expectedCount - 1;

                Assert.AreEqual(result.SegmentIndex, actualIndex,
                    $"Spin #{spin + 1}: pointer at {pointerAngle:F2}° should be segment {result.SegmentIndex} but got {actualIndex}. " +
                    $"currentRotation={currentRotation:F2}, totalRotationDeg={result.TotalRotationDeg:F2}");
            }
        }

        [TestMethod]
        public void Spin_ForceIndex_WithCurrentRotation_LandsCorrectly()
        {
            var template = new RouletteTemplate { Name = "ForceWithRot", SegmentCount = 6 };
            template.Segments = Enumerable.Range(0, 6)
                .Select(i => new RouletteSegment { Index = i, Title = $"Seg {i}" })
                .ToList();

            double currentRotation = 0;
            for (int targetIdx = 0; targetIdx < 6; targetIdx++)
            {
                var result = RouletteService.Spin(template, forceIndex: targetIdx, currentRotation: currentRotation);
                currentRotation += result.TotalRotationDeg;

                double pointerAngle = (360.0 - currentRotation % 360.0) % 360.0;
                if (pointerAngle < 0) pointerAngle += 360.0;

                double segAngle = 360.0 / 6;
                int actualIndex = (int)(pointerAngle / segAngle);
                if (actualIndex >= 6) actualIndex = 5;

                Assert.AreEqual(targetIdx, actualIndex,
                    $"Force index {targetIdx}: pointer at {pointerAngle:F2}° landed on segment {actualIndex}");
            }
        }

        // ── Export / Import ───────────────────────────────────────────────────

        [TestMethod]
        public async Task ExportImport_RoundTripsTemplate()
        {
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

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
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

            await svc.SeedDefaultTemplatesAsync();
            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(3, list.Count);
            Assert.IsTrue(list.All(t => t.IsBuiltIn));
        }

        [TestMethod]
        public async Task SeedDefaultTemplates_DoesNotDuplicateWhenCalledTwice()
        {
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

            await svc.SeedDefaultTemplatesAsync();
            await svc.SeedDefaultTemplatesAsync();
            var list = await svc.ListTemplatesAsync();

            Assert.AreEqual(3, list.Count);
        }
    }
}
