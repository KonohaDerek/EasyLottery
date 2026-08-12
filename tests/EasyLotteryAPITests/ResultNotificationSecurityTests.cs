using EasyLotteryApi.Activities.ActivityResults.Endpoints;

namespace EasyLotteryApiTests;

[TestClass]
public sealed class ResultNotificationSecurityTests
{
    [TestMethod]
    public void RequestContract_DoesNotAcceptSmtpHostOrCredentials()
    {
        var properties = typeof(ResultNotificationRequest).GetProperties().Select(property => property.Name).ToArray();

        CollectionAssert.DoesNotContain(properties, "Smtp");
        CollectionAssert.DoesNotContain(properties, "Host");
        CollectionAssert.DoesNotContain(properties, "Username");
        CollectionAssert.DoesNotContain(properties, "Password");
        CollectionAssert.DoesNotContain(properties, "FromAddress");
        CollectionAssert.DoesNotContain(properties, "Recipient");
    }
}
