using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace EasyLotteryApi;

public sealed class PasskeyAuthenticationService
{
    private readonly AdminPasskeyOptions _options;
    private readonly AdminTokenIssuancePolicy _issuancePolicy;
    private readonly ObsSessionTokenService _tokens;
    private readonly PasskeyStateStore _store;

    public PasskeyAuthenticationService(
        AdminPasskeyOptions options,
        AdminTokenIssuancePolicy issuancePolicy,
        ObsSessionTokenService tokens,
        PasskeyStateStore store)
    {
        _options = options;
        _issuancePolicy = issuancePolicy;
        _tokens = tokens;
        _store = store;
    }

    public async Task<PasskeyBeginResult> BeginAsync(
        HttpContext context,
        PasskeyOptionsRequest request,
        CancellationToken cancellationToken)
    {
        var email = AdminPasskeyOptions.NormalizeEmail(request.Email);
        var flow = (request.Flow ?? "").Trim().ToLowerInvariant();
        if (!_options.IsAdminEmail(email))
            throw new PasskeyAuthenticationException(StatusCodes.Status403Forbidden, "admin_email_required", "只有設定的 admin email 可以登入。");
        if (flow is not (AdminPasskeyOptions.RegisterFlow or AdminPasskeyOptions.LoginFlow))
            throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "invalid_flow", "Passkey flow 不正確。");

        var state = await _store.ReadAsync(cancellationToken);
        if (flow == AdminPasskeyOptions.RegisterFlow)
        {
            if (!_issuancePolicy.CanIssue(context))
                throw new PasskeyAuthenticationException(StatusCodes.Status403Forbidden, "registration_not_allowed", "首次註冊 Passkey 必須從允許的管理來源進行。");
            if (state.Credential is not null)
                throw new PasskeyAuthenticationException(StatusCodes.Status409Conflict, "passkey_already_registered", "管理員已經註冊 Passkey，請直接登入。");
        }
        else if (state.Credential is null)
        {
            throw new PasskeyAuthenticationException(StatusCodes.Status409Conflict, "registration_required", "管理員尚未註冊 Passkey。");
        }

        var fido2 = new Fido2(_options.CreateFido2Configuration());
        var user = CreateUser(email);
        string optionsJson;
        if (flow == AdminPasskeyOptions.RegisterFlow)
        {
            var options = fido2.RequestNewCredential(new RequestNewCredentialParams
            {
                User = user,
                ExcludeCredentials = [],
                AttestationPreference = AttestationConveyancePreference.None
            });
            optionsJson = options.ToJson();
        }
        else
        {
            var descriptor = new PublicKeyCredentialDescriptor(state.Credential!.CredentialId);
            var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = [descriptor],
                UserVerification = UserVerificationRequirement.Required
            });
            optionsJson = options.ToJson();
        }

        await _store.SetPendingAsync(
            flow,
            new PasskeyPendingCeremony
            {
                Email = email,
                OptionsJson = optionsJson,
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(_options.ChallengeLifetime)
            },
            cancellationToken);

        using var document = JsonDocument.Parse(optionsJson);
        return new PasskeyBeginResult(flow, document.RootElement.Clone());
    }

    public async Task<SessionToken> FinishAsync(
        PasskeyVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var email = AdminPasskeyOptions.NormalizeEmail(request.Email);
        var flow = (request.Flow ?? "").Trim().ToLowerInvariant();
        if (!_options.IsAdminEmail(email))
            throw new PasskeyAuthenticationException(StatusCodes.Status403Forbidden, "admin_email_required", "只有設定的 admin email 可以登入。");
        if (flow is not (AdminPasskeyOptions.RegisterFlow or AdminPasskeyOptions.LoginFlow))
            throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "invalid_flow", "Passkey flow 不正確。");

        var pending = await _store.TakePendingAsync(flow, cancellationToken);
        if (pending is null || !string.Equals(pending.Email, email, StringComparison.Ordinal))
            throw new PasskeyAuthenticationException(StatusCodes.Status401Unauthorized, "challenge_invalid", "Passkey challenge 無效或已過期。");

        try
        {
            var fido2 = new Fido2(_options.CreateFido2Configuration());
            if (flow == AdminPasskeyOptions.RegisterFlow)
            {
                var response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(request.Credential.GetRawText())
                    ?? throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "credential_invalid", "Passkey registration response 無效。");
                var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
                {
                    AttestationResponse = response,
                    OriginalOptions = CredentialCreateOptions.FromJson(pending.OptionsJson),
                    IsCredentialIdUniqueToUserCallback = async (_, _) => (await _store.ReadAsync()).Credential is null
                }, cancellationToken);

                await _store.SaveCredentialAsync(new PasskeyCredentialRecord
                {
                    CredentialId = result.Id,
                    PublicKey = result.PublicKey,
                    UserHandle = StableUserHandle(email),
                    SignatureCounter = result.SignCount
                }, cancellationToken);
            }
            else
            {
                var credential = (await _store.ReadAsync()).Credential
                    ?? throw new PasskeyAuthenticationException(StatusCodes.Status409Conflict, "registration_required", "管理員尚未註冊 Passkey。");
                var response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(request.Credential.GetRawText())
                    ?? throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "credential_invalid", "Passkey login response 無效。");
                var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
                {
                    AssertionResponse = response,
                    OriginalOptions = AssertionOptions.FromJson(pending.OptionsJson),
                    StoredPublicKey = credential.PublicKey,
                    StoredSignatureCounter = credential.SignatureCounter,
                    IsUserHandleOwnerOfCredentialIdCallback = async (args, _) =>
                        args.CredentialId.SequenceEqual(credential.CredentialId)
                        && args.UserHandle.SequenceEqual(credential.UserHandle)
                }, cancellationToken);
                await _store.UpdateCounterAsync(result.CredentialId, result.SignCount, cancellationToken);
            }

            return _tokens.IssueAdminToken(email);
        }
        catch (PasskeyAuthenticationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PasskeyAuthenticationException(StatusCodes.Status401Unauthorized, "passkey_verification_failed", "Passkey 驗證失敗。", exception);
        }
    }

    private static Fido2User CreateUser(string email) => new()
    {
        DisplayName = email,
        Name = email,
        Id = StableUserHandle(email)
    };

    private static byte[] StableUserHandle(string email) => SHA256.HashData(Encoding.UTF8.GetBytes(email));
}

public sealed record PasskeyOptionsRequest(string Email, string Flow);
public sealed record PasskeyVerifyRequest(string Email, string Flow, JsonElement Credential);
public sealed record PasskeyBeginResult(string Flow, JsonElement Options);

public sealed class PasskeyAuthenticationException : Exception
{
    public PasskeyAuthenticationException(int statusCode, string code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }
    public string Code { get; }
}
