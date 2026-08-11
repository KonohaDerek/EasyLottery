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
            Assert.AreNotEqual(Guid.Empty, created.PublicId);
            Assert.AreEqual("Test", created.Name);
        }

        [TestMethod]
        public async Task CreateTemplate_ClampsPlaybackDurations()
        {
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

            var created = await svc.CreateTemplateAsync(new RouletteTemplate
            {
                Name = "Playback",
                SpinDurationSec = 99,
                ResultDisplayDurationSeconds = 999
            });

            Assert.AreEqual(30, created.SpinDurationSec);
            Assert.AreEqual(300, created.ResultDisplayDurationSeconds);
        }

        [TestMethod]
        public async Task ListTemplates_AssignsAndPersistsMissingPublicId()
        {
            var store = new InMemoryEasyLotteryConfigStore();
            var svc = new RouletteService(store);
            var created = await svc.CreateTemplateAsync(new RouletteTemplate { Name = "Legacy" });
            created.PublicId = Guid.Empty;

            var migrated = (await svc.ListTemplatesAsync()).Single(template => template.Id == created.Id);
            var reloaded = await svc.LoadTemplateAsync(migrated.PublicId);

            Assert.AreNotEqual(Guid.Empty, migrated.PublicId);
            Assert.IsNotNull(reloaded);
            Assert.AreEqual(created.Id, reloaded.Id);
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

        [TestMethod]
        public async Task DuplicateTemplate_CreatesEditableCopy()
        {
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

            var template = new RouletteTemplate
            {
                Name = "Original",
                Description = "Base wheel",
                SegmentCount = 4,
                SpinDurationSec = 7.5,
                EasingFunction = "ease-in-out",
                InitialAngleDeg = 15,
                CenterImageUrl = "/center.png",
                BackgroundImageUrl = "/bg.png",
                PointerImageUrl = "/pointer.png",
                SpinSoundUrl = "/spin.mp3",
                WinSoundUrl = "/win.mp3",
                Segments = new List<RouletteSegment>
                {
                    new RouletteSegment { Index = 0, Title = "A", ImageUrl = "/a.png", Color = "#111111", Probability = 10 },
                    new RouletteSegment { Index = 1, Title = "B", ImageUrl = "/b.png", Color = "#222222", Probability = 20 }
                }
            };
            var created = await svc.CreateTemplateAsync(template);

            var duplicate = await svc.DuplicateTemplateAsync(created.Id);
            var loaded = await svc.LoadTemplateAsync(duplicate.Id);

            Assert.IsNotNull(loaded);
            Assert.AreNotEqual(created.Id, duplicate.Id);
            Assert.AreNotEqual(created.PublicId, duplicate.PublicId);
            Assert.AreEqual("Original - 複製", duplicate.Name);
            Assert.IsFalse(duplicate.IsBuiltIn);
            Assert.AreEqual(TemplatePublicationStatus.Draft, duplicate.PublicationStatus);
            Assert.AreEqual(template.Description, duplicate.Description);
            Assert.AreEqual(template.SegmentCount, duplicate.SegmentCount);
            Assert.AreEqual(template.SpinDurationSec, duplicate.SpinDurationSec);
            Assert.AreEqual(template.ResultDisplayDurationSeconds, duplicate.ResultDisplayDurationSeconds);
            Assert.AreEqual(template.EasingFunction, duplicate.EasingFunction);
            Assert.AreEqual(template.InitialAngleDeg, duplicate.InitialAngleDeg);
            Assert.AreEqual(template.CenterImageUrl, duplicate.CenterImageUrl);
            Assert.AreEqual(template.BackgroundImageUrl, duplicate.BackgroundImageUrl);
            Assert.AreEqual(template.PointerImageUrl, duplicate.PointerImageUrl);
            Assert.AreEqual(template.SpinSoundUrl, duplicate.SpinSoundUrl);
            Assert.AreEqual(template.WinSoundUrl, duplicate.WinSoundUrl);
            Assert.AreEqual(2, duplicate.Segments.Count);
            Assert.AreEqual("A", duplicate.Segments[0].Title);
            Assert.AreEqual("B", duplicate.Segments[1].Title);
            Assert.AreEqual(10, duplicate.Segments[0].Probability);
            Assert.AreEqual(20, duplicate.Segments[1].Probability);
        }

        [TestMethod]
        public async Task DuplicateBuiltInTemplate_TracksMarketSource()
        {
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

            var builtIn = (await svc.ListTemplatesAsync()).First(template => template.IsBuiltIn);
            var duplicate = await svc.DuplicateTemplateAsync(builtIn.Id);
            var similarlyNamedCustom = await svc.CreateTemplateAsync(new RouletteTemplate { Name = $"{builtIn.Name} - 自訂" });

            Assert.AreEqual(builtIn.PublicId, duplicate.MarketSourcePublicId);
            Assert.AreEqual(Guid.Empty, similarlyNamedCustom.MarketSourcePublicId);
        }

        [TestMethod]
        public async Task SetPublicationStatus_UpdatesTemplateStatus()
        {
            var svc = new RouletteService(new InMemoryEasyLotteryConfigStore());

            var created = await svc.CreateTemplateAsync(new RouletteTemplate { Name = "Status" });

            var updated = await svc.SetPublicationStatusAsync(created.Id, TemplatePublicationStatus.Draft);

            Assert.AreEqual(TemplatePublicationStatus.Draft, updated.PublicationStatus);
            var loaded = await svc.LoadTemplateAsync(created.Id);
            Assert.IsNotNull(loaded);
            Assert.AreEqual(TemplatePublicationStatus.Draft, loaded.PublicationStatus);
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

            Assert.ThrowsExactly<InvalidOperationException>(() => RouletteService.Spin(template));
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
