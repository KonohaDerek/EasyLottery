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
    }
}
