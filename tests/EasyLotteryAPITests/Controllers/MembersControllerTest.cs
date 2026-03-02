using EasyLotteryAPI.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace EasyLotteryAPITests.Controllers
{
    [TestClass]
    public class MembersControllerTest
    {
        [TestMethod]
        public void Controller_HasApiControllerAttribute()
        {
            var attrs = typeof(MembersController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
            Assert.IsTrue(attrs.Length > 0, "MembersController should have [ApiController] attribute");
        }

        [TestMethod]
        public void Controller_HasCorrectRoute()
        {
            var attrs = typeof(MembersController).GetCustomAttributes(typeof(RouteAttribute), true);
            Assert.IsTrue(attrs.Length > 0, "MembersController should have [Route] attribute");

            var routeAttr = (RouteAttribute)attrs[0];
            Assert.AreEqual("api/[controller]", routeAttr.Template);
        }

        [TestMethod]
        public void Controller_InheritsFromControllerBase()
        {
            Assert.IsTrue(typeof(ControllerBase).IsAssignableFrom(typeof(MembersController)));
        }

        [TestMethod]
        public void GetAsync_MethodExists()
        {
            var method = typeof(MembersController).GetMethod("GetAsync");
            Assert.IsNotNull(method, "GetAsync method should exist");
        }

        [TestMethod]
        public void GetAsync_ReturnsCorrectType()
        {
            var method = typeof(MembersController).GetMethod("GetAsync");
            Assert.IsNotNull(method);

            var returnType = method!.ReturnType;
            Assert.IsTrue(returnType.IsGenericType);
            Assert.AreEqual(typeof(Task<>), returnType.GetGenericTypeDefinition());

            var innerType = returnType.GetGenericArguments()[0];
            Assert.IsTrue(innerType.IsGenericType);
            Assert.AreEqual(typeof(List<>), innerType.GetGenericTypeDefinition());
        }

        [TestMethod]
        public void Controller_CanBeInstantiated()
        {
            var controller = new MembersController();
            Assert.IsNotNull(controller);
        }
    }
}
