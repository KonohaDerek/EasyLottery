using System.Security.Cryptography;
using System.Text;
using EasyLotteryApplication.Settings;
using EasyLotteryDomain.Models.Interactions;

namespace EasyLotteryApplication.Interactions;

public sealed record LinkCodeResult(Guid RequestId, string Code, DateTimeOffset ExpiresAtUtc);

public sealed class IdentityLinkService(IInteractionsYamlDocumentRepository repository, TimeProvider clock)
{
    public async Task<LinkCodeResult> IssueAsync(Guid sourceIdentityId, Guid targetIdentityId, CancellationToken cancellationToken = default)
    {
        if (sourceIdentityId == Guid.Empty || targetIdentityId == Guid.Empty || sourceIdentityId == targetIdentityId) throw new InvalidOperationException("綁定身份無效。");
        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var request = new IdentityLinkRequest { SourceIdentityId = sourceIdentityId, TargetIdentityId = targetIdentityId, CodeHash = Hash(code), ExpiresAtUtc = clock.GetUtcNow().AddMinutes(10) };
        var document = await repository.ReadAsync(cancellationToken);
        document.IdentityLinkRequests.RemoveAll(item => item.Consumed || item.ExpiresAtUtc <= clock.GetUtcNow());
        document.IdentityLinkRequests.Add(request);
        await repository.SaveAsync(document, cancellationToken);
        return new LinkCodeResult(request.Id, code, request.ExpiresAtUtc);
    }

    public async Task CompleteAsync(string code, CancellationToken cancellationToken = default)
    {
        var document = await repository.ReadAsync(cancellationToken);
        var candidateHash = Hash(code);
        var request = document.IdentityLinkRequests.SingleOrDefault(item => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(item.CodeHash), Encoding.UTF8.GetBytes(candidateHash)) && !item.Consumed);
        if (request is null || request.ExpiresAtUtc <= clock.GetUtcNow()) throw new InvalidOperationException("綁定碼無效或已過期。");
        var source = document.PlatformIdentities.Single(item => item.Id == request.SourceIdentityId);
        var target = document.PlatformIdentities.Single(item => item.Id == request.TargetIdentityId);
        var sourceProfile = source.AudienceProfileId;
        var targetProfile = target.AudienceProfileId;
        foreach (var identity in document.PlatformIdentities.Where(item => item.AudienceProfileId == sourceProfile)) identity.AudienceProfileId = targetProfile;
        request.Consumed = true;
        document.IdentityMergeAudits.Add(new IdentityMergeAudit { SourceProfileId = sourceProfile, TargetProfileId = targetProfile, Action = "link", Reason = "audience-link-code" });
        await repository.SaveAsync(document, cancellationToken);
    }

    public async Task UnlinkAsync(Guid identityId, string reason, CancellationToken cancellationToken = default)
    {
        if (identityId == Guid.Empty || string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("身份與解除原因皆為必填。");
        var document = await repository.ReadAsync(cancellationToken);
        var identity = document.PlatformIdentities.SingleOrDefault(item => item.Id == identityId) ?? throw new KeyNotFoundException("找不到平台身份。");
        var previousProfile = identity.AudienceProfileId;
        var replacement = new AudienceProfile { DisplayName = identity.DisplayName };
        document.AudienceProfiles.Add(replacement);
        identity.AudienceProfileId = replacement.Id;
        document.IdentityMergeAudits.Add(new IdentityMergeAudit { SourceProfileId = previousProfile, TargetProfileId = replacement.Id, Action = "unlink", Reason = reason.Trim() });
        await repository.SaveAsync(document, cancellationToken);
    }

    public async Task<IReadOnlyList<IdentityMergeAudit>> ListAuditsAsync(CancellationToken cancellationToken = default) =>
        (await repository.ReadAsync(cancellationToken)).IdentityMergeAudits.OrderByDescending(item => item.RecordedAtUtc).ToList();

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
