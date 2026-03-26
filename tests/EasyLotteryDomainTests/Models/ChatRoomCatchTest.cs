using EasyLotteryDomain.Models.Pages;
using System.ComponentModel.DataAnnotations;

namespace EasyLotteryDomainTests.Models
{
    [TestClass]
    public class ChatRoomCatchTest
    {
        private static IList<ValidationResult> ValidateModel(object model)
        {
            var results = new List<ValidationResult>();
            var context = new ValidationContext(model, null, null);
            Validator.TryValidateObject(model, context, results, true);
            return results;
        }

        [TestMethod]
        public void DefaultValues_AreCorrect()
        {
            var model = new ChatRoomCatch();
            Assert.AreEqual("", model.ID);
            Assert.AreEqual("", model.YoutubeUrl);
            Assert.AreEqual("", model.KeyWord);
            Assert.AreEqual("", model.WhiteList);
            Assert.AreEqual("", model.BlackList);
            Assert.AreEqual(0, model.CooldownSeconds);
            Assert.IsTrue(model.LastMessageTime <= DateTimeOffset.Now);
        }

        [TestMethod]
        public void Validation_MissingYoutubeUrl_Fails()
        {
            var model = new ChatRoomCatch
            {
                YoutubeUrl = "",
                KeyWord = "test"
            };

            var results = ValidateModel(model);
            Assert.IsTrue(results.Any(r => r.ErrorMessage!.Contains("YT 網址")));
        }

        [TestMethod]
        public void Validation_MissingKeyWord_Fails()
        {
            var model = new ChatRoomCatch
            {
                YoutubeUrl = "https://www.youtube.com/watch?v=test123",
                KeyWord = ""
            };

            var results = ValidateModel(model);
            Assert.IsTrue(results.Any(r => r.ErrorMessage!.Contains("通關密語")));
        }

        [TestMethod]
        public void Validation_AllFieldsProvided_Passes()
        {
            var model = new ChatRoomCatch
            {
                YoutubeUrl = "https://www.youtube.com/watch?v=test123",
                KeyWord = "密語"
            };

            var results = ValidateModel(model);
            Assert.AreEqual(0, results.Count);
        }

        [TestMethod]
        public void SetProperties_ValuesAreStored()
        {
            var now = DateTimeOffset.UtcNow;
            var model = new ChatRoomCatch
            {
                ID = "chat-123",
                YoutubeUrl = "https://www.youtube.com/live/abc",
                KeyWord = "抽獎",
                LastMessageTime = now
            };

            Assert.AreEqual("chat-123", model.ID);
            Assert.AreEqual("https://www.youtube.com/live/abc", model.YoutubeUrl);
            Assert.AreEqual("抽獎", model.KeyWord);
            Assert.AreEqual(now, model.LastMessageTime);
        }

        [TestMethod]
        public void Moderation_AllowsMatchingWhitelistAndKeyword()
        {
            var model = new ChatRoomCatch
            {
                KeyWord = "抽獎",
                WhiteList = "Alice,Bob",
                BlackList = "Eve",
                CooldownSeconds = 30
            };

            var previous = new Dictionary<string, DateTimeOffset>
            {
                { "Alice", DateTimeOffset.UtcNow.AddMinutes(-1) }
            };

            var allowed = model.IsMessageAllowed("Alice", "我想抽獎", DateTimeOffset.UtcNow, previous, out var reason);

            Assert.IsTrue(allowed);
            Assert.AreEqual("", reason);
        }

        [TestMethod]
        public void Moderation_RejectsNonWhitelistedUser()
        {
            var model = new ChatRoomCatch
            {
                KeyWord = "抽獎",
                WhiteList = "Alice,Bob"
            };

            var allowed = model.IsMessageAllowed("Charlie", "我想抽獎", DateTimeOffset.UtcNow, new Dictionary<string, DateTimeOffset>(), out var reason);

            Assert.IsFalse(allowed);
            Assert.AreEqual("不在白名單", reason);
        }

        [TestMethod]
        public void Moderation_RejectsBlacklistedUser()
        {
            var model = new ChatRoomCatch
            {
                KeyWord = "抽獎",
                BlackList = "Eve"
            };

            var allowed = model.IsMessageAllowed("Eve", "我要抽獎", DateTimeOffset.UtcNow, new Dictionary<string, DateTimeOffset>(), out var reason);

            Assert.IsFalse(allowed);
            Assert.AreEqual("命中黑名單", reason);
        }

        [TestMethod]
        public void Moderation_RejectsWithinCooldown()
        {
            var model = new ChatRoomCatch
            {
                KeyWord = "抽獎",
                CooldownSeconds = 60
            };

            var previous = new Dictionary<string, DateTimeOffset>
            {
                { "Alice", DateTimeOffset.UtcNow.AddSeconds(-10) }
            };

            var allowed = model.IsMessageAllowed("Alice", "我要抽獎", DateTimeOffset.UtcNow, previous, out var reason);

            Assert.IsFalse(allowed);
            Assert.AreEqual("同帳號冷卻中", reason);
        }

        [TestMethod]
        public void KeywordMatching_SupportsMultipleRules()
        {
            var model = new ChatRoomCatch
            {
                KeyWord = "抽獎,抽我,得獎"
            };

            Assert.IsTrue(model.MatchesKeyword("我要抽獎"));
            Assert.IsTrue(model.MatchesKeyword("請抽我"));
            Assert.IsTrue(model.MatchesKeyword("恭喜得獎"));
            Assert.IsFalse(model.MatchesKeyword("只是路過"));
        }
    }
}
