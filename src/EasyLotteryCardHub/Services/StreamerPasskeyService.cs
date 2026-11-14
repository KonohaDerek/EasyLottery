using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace EasyLotteryCardHub.Services;

public sealed class StreamerPasskeyService(
    HubDbContext db,
    StreamerPasskeyOptions passkeyOptions,
    PasskeyCeremonyStore ceremonyStore,
    InviteRedemptionService inviteRedemption,
    HubTokenService tokenService,
    TimeProvider timeProvider)
{
    public async Task<PasskeyBeginResponse> BeginRegistrationAsync(
        string? inviteToken,
        string? displayName,
        string? slug,
        CancellationToken cancellationToken)
    {
        var token = inviteToken?.Trim() ?? string.Empty;
        if (token.Length is < 32 or > 160)
            throw InvalidInvite();
        var tokenHash = InviteRedemptionService.HashInviteToken(token);
        var now = timeProvider.GetUtcNow();
        var invite = await db.StreamerInvites.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash && item.RedeemedAt == null && item.ExpiresAt > now, cancellationToken);
        if (invite is null) throw InvalidInvite();

        var normalizedDisplayName = HubValidation.NormalizeDisplayName(displayName);
        var normalizedSlug = HubValidation.NormalizeSlug(slug);
        if (await db.Streamers.AnyAsync(item => item.EmailNormalized == invite.EmailNormalized || item.Slug == normalizedSlug, cancellationToken))
            throw new HubServiceException(StatusCodes.Status409Conflict, "tenant_already_exists", "A tenant already exists for this email or slug.");

        var user = CreateUser(invite.EmailNormalized, normalizedDisplayName);
        var optionsJson = new Fido2(passkeyOptions.CreateFido2Configuration()).RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            AttestationPreference = AttestationConveyancePreference.None
        }).ToJson();
        var flowId = Guid.NewGuid();
        ceremonyStore.Put(flowId, new PendingPasskeyCeremony(
            "register", optionsJson, now.Add(passkeyOptions.ChallengeLifetime), invite.EmailNormalized, tokenHash, normalizedDisplayName, normalizedSlug));
        return new PasskeyBeginResponse(flowId, ParseOptions(optionsJson));
    }

    public async Task<IssuedStreamerRegistration> FinishRegistrationAsync(Guid flowId, JsonElement credential, CancellationToken cancellationToken)
    {
        if (!ceremonyStore.TryTake(flowId, out var pending) || pending.Kind != "register"
            || pending.InviteTokenHash is null || pending.EmailNormalized is null)
            throw InvalidChallenge();

        PasskeyCredentialResult result;
        try
        {
            var response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(credential.GetRawText())
                ?? throw new HubServiceException(StatusCodes.Status400BadRequest, "credential_invalid", "The Passkey registration response is invalid.");
            var fido2 = new Fido2(passkeyOptions.CreateFido2Configuration());
            var registration = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = CredentialCreateOptions.FromJson(pending.OptionsJson),
                IsCredentialIdUniqueToUserCallback = async (parameters, _) =>
                    !await db.StreamerPasskeys.AnyAsync(item => item.CredentialId == parameters.CredentialId, cancellationToken)
            }, cancellationToken);
            result = new PasskeyCredentialResult(
                registration.Id,
                registration.PublicKey,
                StableUserHandle(pending.EmailNormalized),
                checked((int)Math.Min((long)registration.SignCount, int.MaxValue)));
        }
        catch (HubServiceException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new HubServiceException(StatusCodes.Status400BadRequest, "passkey_verification_failed", "Passkey verification failed.");
        }

        var tenant = await inviteRedemption.RedeemAsync(
            pending.InviteTokenHash,
            pending.DisplayName,
            pending.Slug,
            result.CredentialId,
            result.PublicKey,
            result.UserHandle,
            result.SignatureCounter,
            cancellationToken);
        return new IssuedStreamerRegistration(tenant.Id, tokenService.IssueStreamerToken(tenant.Id));
    }

    public async Task<PasskeyBeginResponse> BeginLoginAsync(string? email, CancellationToken cancellationToken)
    {
        var normalizedEmail = HubValidation.NormalizeEmail(email);
        var tenant = await db.Streamers.AsNoTracking().SingleOrDefaultAsync(item => item.EmailNormalized == normalizedEmail, cancellationToken);
        var passkeys = tenant is null
            ? []
            : await db.StreamerPasskeys.AsNoTracking().Where(item => item.TenantId == tenant.Id).ToListAsync(cancellationToken);
        var allowedCredentials = passkeys.Count == 0
            ? [new PublicKeyCredentialDescriptor(RandomNumberGenerator.GetBytes(32))]
            : passkeys.Select(item => new PublicKeyCredentialDescriptor(item.CredentialId)).ToList();
        var optionsJson = new Fido2(passkeyOptions.CreateFido2Configuration()).GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Required
        }).ToJson();
        var flowId = Guid.NewGuid();
        ceremonyStore.Put(flowId, new PendingPasskeyCeremony(
            "login", optionsJson, timeProvider.GetUtcNow().Add(passkeyOptions.ChallengeLifetime), normalizedEmail, TenantId: tenant?.Id));
        return new PasskeyBeginResponse(flowId, ParseOptions(optionsJson));
    }

    public async Task<IssuedHubToken> FinishLoginAsync(Guid flowId, JsonElement credential, CancellationToken cancellationToken)
    {
        if (!ceremonyStore.TryTake(flowId, out var pending) || pending.Kind != "login"
            || pending.TenantId is null || pending.EmailNormalized is null)
            throw LoginFailed();

        var credentialId = ReadCredentialId(credential);
        var stored = await db.StreamerPasskeys.SingleOrDefaultAsync(
            item => item.TenantId == pending.TenantId && item.CredentialId == credentialId,
            cancellationToken);
        if (stored is null) throw LoginFailed();

        uint newSignatureCounter;
        try
        {
            var response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(credential.GetRawText())
                ?? throw LoginFailed();
            var result = await new Fido2(passkeyOptions.CreateFido2Configuration()).MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = response,
                OriginalOptions = AssertionOptions.FromJson(pending.OptionsJson),
                StoredPublicKey = stored.PublicKey,
                StoredSignatureCounter = checked((uint)Math.Max(0, stored.SignatureCounter)),
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                    args.CredentialId.SequenceEqual(stored.CredentialId) && args.UserHandle.SequenceEqual(stored.UserHandle))
            }, cancellationToken);
            newSignatureCounter = result.SignCount;
        }
        catch (Exception)
        {
            throw LoginFailed();
        }

        stored.SignatureCounter = checked((int)Math.Min((long)newSignatureCounter, int.MaxValue));
        var tenantStatus = await db.Streamers.Where(item => item.Id == stored.TenantId)
            .Select(item => (TenantStatus?)item.Status).SingleOrDefaultAsync(cancellationToken);
        if (tenantStatus is null) throw LoginFailed();
        if (tenantStatus != TenantStatus.Active) throw TenantAccessErrors.AsException(tenantStatus.Value);
        await db.SaveChangesAsync(cancellationToken);
        return tokenService.IssueStreamerToken(stored.TenantId);
    }

    private static Fido2User CreateUser(string email, string displayName) => new()
    {
        Name = email,
        DisplayName = displayName,
        Id = StableUserHandle(email)
    };

    private static byte[] StableUserHandle(string email) => SHA256.HashData(Encoding.UTF8.GetBytes(email));

    private static JsonElement ParseOptions(string optionsJson)
    {
        using var document = JsonDocument.Parse(optionsJson);
        return document.RootElement.Clone();
    }

    private static byte[] ReadCredentialId(JsonElement credential)
    {
        if (credential.ValueKind != JsonValueKind.Object
            || !credential.TryGetProperty("rawId", out var rawId)
            || string.IsNullOrWhiteSpace(rawId.GetString()))
            throw LoginFailed();

        try
        {
            return WebEncoders.Base64UrlDecode(rawId.GetString()!);
        }
        catch (FormatException)
        {
            throw LoginFailed();
        }
    }

    private static HubServiceException InvalidInvite() =>
        new(StatusCodes.Status410Gone, "invite_invalid_or_expired", "The invite is invalid, expired, or has already been redeemed.");

    private static HubServiceException InvalidChallenge() =>
        new(StatusCodes.Status401Unauthorized, "challenge_invalid", "The Passkey challenge is invalid or expired.");

    private static HubServiceException LoginFailed() =>
        new(StatusCodes.Status401Unauthorized, "passkey_verification_failed", "Passkey verification failed.");
}

public sealed record PasskeyBeginResponse(Guid FlowId, JsonElement Options);
public sealed record IssuedStreamerRegistration(Guid TenantId, IssuedHubToken Token);
public sealed record PasskeyCredentialResult(byte[] CredentialId, byte[] PublicKey, byte[] UserHandle, int SignatureCounter);
