using EasyLotteryAPI.Services;

namespace EasyLotteryAPITests.Services
{
    [TestClass]
    public class DemoServiceTest
    {
        [TestMethod]
        public void DemoService_ClassExists()
        {
            var type = typeof(DemoService);
            Assert.IsNotNull(type);
        }

        [TestMethod]
        public void GetChannelMembers_IsStaticMethod()
        {
            var method = typeof(DemoService).GetMethod("GetChannelMembers");
            Assert.IsNotNull(method, "GetChannelMembers method should exist");
            Assert.IsTrue(method!.IsStatic, "GetChannelMembers should be a static method");
        }

        [TestMethod]
        public void GetChannelMembers_ReturnsTaskOfList()
        {
            var method = typeof(DemoService).GetMethod("GetChannelMembers");
            Assert.IsNotNull(method);

            var returnType = method!.ReturnType;
            Assert.IsTrue(returnType.IsGenericType);
            Assert.AreEqual(typeof(Task<>), returnType.GetGenericTypeDefinition());

            var innerType = returnType.GetGenericArguments()[0];
            Assert.IsTrue(innerType.IsGenericType);
            Assert.AreEqual(typeof(List<>), innerType.GetGenericTypeDefinition());
        }

        [TestMethod]
        [Ignore("Depends on external Google OAuth / network and can hang in CI; keep as manual integration coverage.")]
        public async Task GetChannelMembers_WithInvalidCredentials_ReturnsEmptyList()
        {
            // DemoService 使用硬編碼的憑證，在沒有有效授權的環境中應回傳空清單
            var result = await DemoService.GetChannelMembers();
            Assert.IsNotNull(result);
            // 因為無法真正授權，預期會進入 catch 並回傳空清單
            Assert.IsInstanceOfType(result, typeof(List<Google.Apis.YouTube.v3.Data.Member>));
        }
    }
}
