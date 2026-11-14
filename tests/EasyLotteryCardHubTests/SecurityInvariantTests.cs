using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using EasyLotteryCardHub.Domain;
using EasyLotteryCardHub.Security;
using EasyLotteryCardHub.Invites;
using EasyLotteryCardHub.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace EasyLotteryCardHubTests;

[TestClass]
public sealed class SecurityInvariantTests
{
    [TestMethod]
    public void SiteSecret_IsRandomAndOnlyHashIsNeededForVerification()
    {
        var first = SiteSecret.Create();
        var second = SiteSecret.Create();

        Assert.AreNotEqual(first.ClientId, second.ClientId);
        Assert.AreNotEqual(first.Secret, second.Secret);
        Assert.AreEqual(32, first.Hash.Length);
        Assert.IsTrue(SiteSecret.Verify(first.Secret, first.Hash));
        Assert.IsFalse(SiteSecret.Verify(second.Secret, first.Hash));
    }

    [TestMethod]
    public void SiteAccessToken_ContainsOnlyScopedSiteIdentity()
    {
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CardHub:SiteToken:SigningKey"] = Convert.ToBase64String(key),
            ["CardHub:SiteToken:Issuer"] = "test-card-hub"
        }).Build();
        var issuer = new SiteAccessTokenIssuer(configuration, TimeProvider.System);
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var issued = issuer.Issue(tenantId, siteId, "client-one");

        var handler = new JwtSecurityTokenHandler();
        _ = handler.ValidateToken(issued.Token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "test-card-hub",
            ValidateAudience = true,
            ValidAudience = SiteAccessTokenIssuer.Audience,
            ValidateLifetime = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero
        }, out var validatedToken);
        var claims = ((JwtSecurityToken)validatedToken).Claims;

        Assert.AreEqual("client-one", claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Sub)?.Value);
        Assert.AreEqual(tenantId.ToString("D"), claims.FirstOrDefault(claim => claim.Type == "tenant_id")?.Value);
        Assert.AreEqual(siteId.ToString("D"), claims.FirstOrDefault(claim => claim.Type == "site_id")?.Value);
        Assert.AreEqual("site", claims.FirstOrDefault(claim => claim.Type == "role")?.Value);
        Assert.AreEqual("client-one", claims.FirstOrDefault(claim => claim.Type == "client_id")?.Value);
        Assert.IsTrue(issuer.TryValidateSiteToken(issued.Token, out var verifiedTenant, out var verifiedSite, out var verifiedClient));
        Assert.AreEqual(tenantId, verifiedTenant);
        Assert.AreEqual(siteId, verifiedSite);
        Assert.AreEqual("client-one", verifiedClient);
        Assert.AreEqual(900, issued.ExpiresIn);
        Assert.IsFalse(claims.Any(claim => claim.Type.Contains("viewer", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void SiteAccessToken_RejectsMissingOrWeakSigningKey()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var issuer = new SiteAccessTokenIssuer(configuration, TimeProvider.System);
        Assert.ThrowsExactly<InvalidOperationException>(() => issuer.Issue(Guid.NewGuid(), Guid.NewGuid(), "client"));

        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CardHub:SiteToken:SigningKey"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("short"))
        }).Build();
        issuer = new SiteAccessTokenIssuer(configuration, TimeProvider.System);
        Assert.ThrowsExactly<InvalidOperationException>(() => issuer.Issue(Guid.NewGuid(), Guid.NewGuid(), "client"));
    }

    [TestMethod]
    public void StreamerToken_IsBoundToTheStreamerAudienceAndIdentity()
    {
        var key = Enumerable.Range(32, 32).Select(value => (byte)value).ToArray();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CardHub:SiteToken:SigningKey"] = Convert.ToBase64String(key),
            ["CardHub:SiteToken:Issuer"] = "test-card-hub"
        }).Build();
        var issuer = new SiteAccessTokenIssuer(configuration, TimeProvider.System);
        var tenantId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var token = issuer.IssueStreamer(tenantId, accountId).Token;

        Assert.IsTrue(issuer.TryValidateStreamerToken(token, out var actualTenant, out var actualAccount));
        Assert.AreEqual(tenantId, actualTenant);
        Assert.AreEqual(accountId, actualAccount);
        Assert.IsFalse(issuer.TryValidateStreamerToken(issuer.Issue(tenantId, Guid.NewGuid(), "site").Token, out _, out _));
    }

    [TestMethod]
    public void InviteEmailCipher_ProtectsPayloadAndRejectsTampering()
    {
        var key = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CardHub:EmailOutbox:EncryptionKey"] = Convert.ToBase64String(key)
        }).Build();
        var cipher = new InviteEmailCipher(configuration);
        var message = new InviteEmailPayload("streamer@example.test", "Card Hub invite", "secret-token", DateTimeOffset.UtcNow.AddHours(72), "Streamer");
        var cleartext = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message);
        var protectedPayload = cipher.Protect(cleartext);

        Assert.IsFalse(System.Text.Encoding.UTF8.GetString(protectedPayload).Contains(message.Recipient, StringComparison.Ordinal));
        CollectionAssert.AreEqual(cleartext, cipher.Unprotect(protectedPayload));
        protectedPayload[^1] ^= 1;
        Assert.ThrowsExactly<System.Security.Cryptography.AuthenticationTagMismatchException>(() => cipher.Unprotect(protectedPayload));
    }

    [TestMethod]
    public void DisabledTenantCannotPublishDrawOrTradeButRemainsReadable()
    {
        Assert.IsTrue(TenantStatus.Active.CanPublish());
        Assert.IsFalse(TenantStatus.Disabled.CanPublish());
        Assert.IsFalse(TenantStatus.Disabled.CanDraw());
        Assert.IsFalse(TenantStatus.Disabled.CanTrade());
        Assert.IsTrue(TenantStatus.Disabled.CanViewExistingCards());
        Assert.IsTrue(TenantStatus.Deleted.CanViewExistingCards());
    }

    [TestMethod]
    public void EmailIdentityUsesKeyedHmacAndRequiresStrongKey()
    {
        var key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CardHub:Identity:HmacKey"] = Convert.ToBase64String(key)
        }).Build();
        var hasher = new IdentityHasher(config);
        var expected = System.Security.Cryptography.HMACSHA256.HashData(key, System.Text.Encoding.UTF8.GetBytes("owner@example.test"));
        CollectionAssert.AreEqual(expected, hasher.HashEmail("owner@example.test"));
        Assert.ThrowsExactly<InvalidOperationException>(() => new IdentityHasher(new ConfigurationBuilder().Build()).HashEmail("owner@example.test"));
    }

    [TestMethod]
    public void ProductionPasskeysRequireExplicitHttpsRelyingPartyConfiguration()
    {
        var configuration = new ConfigurationBuilder().Build();
        Assert.ThrowsExactly<InvalidOperationException>(() => PasskeyConfiguration.ValidateProduction(configuration));
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CardHub:Passkey:RpId"] = "cards.example.test",
            ["CardHub:Passkey:Origins:0"] = "http://localhost:8080"
        }).Build();
        Assert.ThrowsExactly<InvalidOperationException>(() => PasskeyConfiguration.ValidateProduction(configuration));
    }

    [TestMethod]
    public void TenantDeletionCannotSkipThirtyDayPendingPeriod()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.IsFalse(TenantStatusPolicy.CanTransition(TenantStatus.Active, TenantStatus.Deleted, null, now));
        Assert.IsFalse(TenantStatusPolicy.CanTransition(TenantStatus.PendingDeletion, TenantStatus.Deleted, now.AddDays(-29), now));
        Assert.IsTrue(TenantStatusPolicy.CanTransition(TenantStatus.PendingDeletion, TenantStatus.Deleted, now.AddDays(-30), now));
        Assert.IsFalse(TenantStatusPolicy.CanTransition(TenantStatus.Deleted, TenantStatus.Active, now.AddDays(-40), now));
    }

    [TestMethod]
    public void StreamerPasskeysUseResidentCredentialsAndEmaillessAllowCredentials()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        using var assertionJson = JsonDocument.Parse(PasskeyConfiguration.CreateDiscoverableAssertionOptions(configuration, true).ToJson());
        var assertion = assertionJson.RootElement;
        Assert.AreEqual("required", assertion.GetProperty("userVerification").GetString());
        Assert.IsFalse(assertion.TryGetProperty("allowCredentials", out var allowed) && allowed.EnumerateArray().Any());

        using var registrationJson = JsonDocument.Parse(PasskeyConfiguration.CreateResidentCredentialOptions(configuration, true,
            Guid.NewGuid(), "streamer", "Streamer").ToJson());
        var selection = registrationJson.RootElement.GetProperty("authenticatorSelection");
        Assert.AreEqual("required", selection.GetProperty("residentKey").GetString());
        Assert.AreEqual("required", selection.GetProperty("userVerification").GetString());
    }
}
