using System.IdentityModel.Tokens.Jwt;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Hosting;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Services;
using Microsoft.Extensions.Configuration;

namespace EasyLotteryCardHub.Tests;

[TestClass]
[TestCategory("Unit")]
public sealed class HubSecurityUnitTests
{
    [TestMethod]
    public void SiteSecretsAreHashedAndComparedWithoutReturningRawValues()
    {
        var secret = SecretHasher.CreateSiteSecret();
        var hash = SecretHasher.Hash(secret);
        Assert.AreEqual(64, hash.Length);
        Assert.IsTrue(SecretHasher.Verify(secret, hash));
        Assert.IsFalse(SecretHasher.Verify(secret + "x", hash));
        Assert.IsFalse(string.Equals(secret, hash, StringComparison.Ordinal));
    }

    [TestMethod]
    public void TenantLifecycleUsesOnlyTheFourD11WireValues()
    {
        Assert.IsTrue(TenantStatuses.TryParse("active", out var active));
        Assert.AreEqual(TenantStatus.Active, active);
        Assert.IsTrue(TenantStatuses.TryParse("disabled", out var disabled));
        Assert.AreEqual(TenantStatus.Disabled, disabled);
        Assert.IsTrue(TenantStatuses.TryParse("pending_deletion", out var pending));
        Assert.AreEqual(TenantStatus.PendingDeletion, pending);
        Assert.IsTrue(TenantStatuses.TryParse("deleted", out var deleted));
        Assert.AreEqual(TenantStatus.Deleted, deleted);
        Assert.IsFalse(TenantStatuses.TryParse("viewer", out _));
    }

    [TestMethod]
    public void TokenIssuersUseDistinctAudiencesAndReserveViewerAudience()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:SigningKey"] = "unit-test-signing-secret-with-at-least-32-bytes",
            ["Authentication:Issuer"] = "https://card-hub.test"
        }).Build();
        var options = HubTokenSecurityOptions.Load(configuration);
        var tokens = new HubTokenService(options, new MutableTimeProvider(DateTimeOffset.UtcNow));
        var tenantId = Guid.NewGuid();
        var streamer = new JwtSecurityTokenHandler().ReadJwtToken(tokens.IssueStreamerToken(tenantId).AccessToken);
        var site = new JwtSecurityTokenHandler().ReadJwtToken(tokens.IssueSiteToken(tenantId, Guid.NewGuid(), Guid.NewGuid()).AccessToken);
        Assert.AreEqual(HubTokenAudiences.Streamer, streamer.Audiences.Single());
        Assert.AreEqual(HubTokenAudiences.Site, site.Audiences.Single());
        CollectionAssert.Contains(HubTokenAudiences.All.ToArray(), HubTokenAudiences.Viewer);
    }

    [TestMethod]
    public void EmailAndSlugValidationNormalizeSafeValues()
    {
        Assert.AreEqual("streamer@example.test", HubValidation.NormalizeEmail(" Streamer@Example.test "));
        Assert.AreEqual("sample-streamer-1", HubValidation.NormalizeSlug("Sample-Streamer-1"));
        Assert.ThrowsExactly<HubServiceException>(() => HubValidation.NormalizeEmail("not-an-email"));
        Assert.ThrowsExactly<HubServiceException>(() => HubValidation.NormalizeSlug("invalid slug"));
    }

    [TestMethod]
    public void ProductionConfigurationRequiresPublicHttpsOriginsAndIssuer()
    {
        var values = new Dictionary<string, string?>
        {
            ["PlatformAdmin:BootstrapEmail"] = "admin@example.test",
            ["PlatformAdmin:BootstrapToken"] = "unit-test-admin-secret-with-at-least-32-bytes",
            ["Authentication:Issuer"] = "https://card-hub.example.test",
            ["Passkey:RpId"] = "example.test",
            ["Passkey:Origins:0"] = "https://hub.example.test"
        };
        var validConfiguration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        HubHostingExtensions.ValidateProductionConfiguration(validConfiguration);

        values["Passkey:Origins:0"] = "http://hub.example.test";
        var invalidConfiguration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        Assert.ThrowsExactly<InvalidOperationException>(() => HubHostingExtensions.ValidateProductionConfiguration(invalidConfiguration));
    }
}
