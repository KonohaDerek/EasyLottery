using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Fido2NetLib;
using Fido2NetLib.Objects;
using EasyLotteryApi.Security;
using Microsoft.AspNetCore.WebUtilities;

namespace EasyLotteryApi.Passkey;

public sealed class PasskeyAuthenticationService
{
    private const string DefaultPasskeyName = "Admin Passkey";
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
        var email = ValidateEmail(request.Email);
        var flow = NormalizeFlow(request.Flow);
        var state = await _store.ReadAsync(cancellationToken);

        if (flow == PasskeyFlow.Register)
        {
            if (!_issuancePolicy.CanIssue(context))
                throw new PasskeyAuthenticationException(StatusCodes.Status403Forbidden, "registration_not_allowed", "首次註冊 Passkey 必須從允許的管理來源進行。");
            if (state.Credentials.Count > 0)
                throw new PasskeyAuthenticationException(StatusCodes.Status409Conflict, "passkey_already_registered", "管理員已經註冊 Passkey，請直接登入。");
        }
        else if (state.Credentials.Count == 0)
        {
            throw new PasskeyAuthenticationException(StatusCodes.Status409Conflict, "registration_required", "管理員尚未註冊 Passkey。");
        }

        var optionsJson = CreateOptionsJson(email, flow, state.Credentials);
        await _store.SetPendingAsync(
            flow,
            new PasskeyPendingCeremony
            {
                Email = email,
                OptionsJson = optionsJson,
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(_options.ChallengeLifetime),
                Name = DefaultPasskeyName
            },
            cancellationToken);

        return new PasskeyBeginResult(flow.ToValue(), ParseOptions(optionsJson));
    }

    public async Task<PasskeyBeginResult> BeginAddAsync(string? name, CancellationToken cancellationToken)
    {
        var state = await _store.ReadAsync(cancellationToken);
        if (state.Credentials.Count == 0)
            throw new PasskeyAuthenticationException(StatusCodes.Status409Conflict, "registration_required", "請先註冊第一個 Passkey。");

        var optionsJson = CreateOptionsJson(_options.Email, PasskeyFlow.Add, state.Credentials);
        await _store.SetPendingAsync(
            PasskeyFlow.Add,
            new PasskeyPendingCeremony
            {
                Email = _options.Email,
                OptionsJson = optionsJson,
                ExpiresAtUtc = DateTimeOffset.UtcNow.Add(_options.ChallengeLifetime),
                Name = NormalizePasskeyName(name)
            },
            cancellationToken);

        return new PasskeyBeginResult(PasskeyFlow.Add.ToValue(), ParseOptions(optionsJson));
    }

    public async Task<SessionToken> FinishAsync(
        PasskeyVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var email = ValidateEmail(request.Email);
        var flow = NormalizeFlow(request.Flow);
        var pending = await _store.TakePendingAsync(flow, cancellationToken);
        if (pending is null || !string.Equals(pending.Email, email, StringComparison.Ordinal))
            throw new PasskeyAuthenticationException(StatusCodes.Status401Unauthorized, "challenge_invalid", "Passkey challenge 無效或已過期。");

        try
        {
            if (flow == PasskeyFlow.Register)
            {
                var credential = await VerifyRegistrationAsync(pending, request.Credential, pending.Name, cancellationToken);
                await _store.AddCredentialAsync(credential, cancellationToken);
            }
            else
            {
                var state = await _store.ReadAsync(cancellationToken);
                var credentialId = ReadCredentialId(request.Credential);
                var credential = state.Credentials.FirstOrDefault(item => item.CredentialId.SequenceEqual(credentialId))
                    ?? throw new PasskeyAuthenticationException(StatusCodes.Status401Unauthorized, "passkey_not_registered", "這個 Passkey 尚未註冊。");
                var response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(request.Credential.GetRawText())
                    ?? throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "credential_invalid", "Passkey login response 無效。");
                var fido2 = new Fido2(_options.CreateFido2Configuration());
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

    public async Task<PasskeyDeviceResponse> FinishAddAsync(
        PasskeyManageVerifyRequest request,
        CancellationToken cancellationToken)
    {
        var pending = await _store.TakePendingAsync(PasskeyFlow.Add, cancellationToken);
        if (pending is null || !string.Equals(pending.Email, _options.Email, StringComparison.Ordinal))
            throw new PasskeyAuthenticationException(StatusCodes.Status401Unauthorized, "challenge_invalid", "Passkey challenge 無效或已過期。");

        try
        {
            var credential = await VerifyRegistrationAsync(pending, request.Credential, pending.Name, cancellationToken);
            await _store.AddCredentialAsync(credential, cancellationToken);
            return ToDeviceResponse(credential);
        }
        catch (PasskeyAuthenticationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "passkey_verification_failed", "Passkey 驗證失敗。", exception);
        }
    }

    private async Task<PasskeyCredentialRecord> VerifyRegistrationAsync(
        PasskeyPendingCeremony pending,
        JsonElement credential,
        string? name,
        CancellationToken cancellationToken)
    {
        var response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(credential.GetRawText())
            ?? throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "credential_invalid", "Passkey registration response 無效。");
        var fido2 = new Fido2(_options.CreateFido2Configuration());
        var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = response,
            OriginalOptions = CredentialCreateOptions.FromJson(pending.OptionsJson),
            IsCredentialIdUniqueToUserCallback = async (parameters, _) =>
            {
                var state = await _store.ReadAsync(cancellationToken);
                return state.Credentials.All(item => !item.CredentialId.SequenceEqual(parameters.CredentialId));
            }
        }, cancellationToken);

        return new PasskeyCredentialRecord
        {
            CredentialId = result.Id,
            PublicKey = result.PublicKey,
            UserHandle = StableUserHandle(pending.Email),
            SignatureCounter = result.SignCount,
            Name = NormalizePasskeyName(name),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private string CreateOptionsJson(
        string email,
        PasskeyFlow flow,
        IReadOnlyCollection<PasskeyCredentialRecord> credentials)
    {
        var fido2 = new Fido2(_options.CreateFido2Configuration());
        var user = CreateUser(email);
        if (flow == PasskeyFlow.Login)
        {
            return fido2.GetAssertionOptions(new GetAssertionOptionsParams
            {
                AllowedCredentials = credentials
                    .Select(item => new PublicKeyCredentialDescriptor(item.CredentialId))
                    .ToList(),
                UserVerification = UserVerificationRequirement.Required
            }).ToJson();
        }

        return fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = user,
            ExcludeCredentials = credentials
                .Select(item => new PublicKeyCredentialDescriptor(item.CredentialId))
                .ToList(),
            AttestationPreference = AttestationConveyancePreference.None
        }).ToJson();
    }

    private string ValidateEmail(string? email)
    {
        var normalized = AdminPasskeyOptions.NormalizeEmail(email);
        if (!_options.IsAdminEmail(normalized))
            throw new PasskeyAuthenticationException(StatusCodes.Status403Forbidden, "admin_email_required", "只有設定的 admin email 可以登入。");
        return normalized;
    }

    private static PasskeyFlow NormalizeFlow(string? flow)
    {
        if (!PasskeyFlows.TryParse(flow, out var normalized) || !PasskeyFlows.Authentication.Contains(normalized))
            throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "invalid_flow", "Passkey flow 不正確。");
        return normalized;
    }

    private static JsonElement ParseOptions(string optionsJson)
    {
        using var document = JsonDocument.Parse(optionsJson);
        return document.RootElement.Clone();
    }

    private static byte[] ReadCredentialId(JsonElement credential)
    {
        if (credential.ValueKind != JsonValueKind.Object
            || !credential.TryGetProperty("rawId", out var rawIdElement)
            || string.IsNullOrWhiteSpace(rawIdElement.GetString()))
        {
            throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "credential_invalid", "Passkey credential ID 無效。");
        }

        try
        {
            return WebEncoders.Base64UrlDecode(rawIdElement.GetString()!);
        }
        catch (FormatException exception)
        {
            throw new PasskeyAuthenticationException(StatusCodes.Status400BadRequest, "credential_invalid", "Passkey credential ID 無效。", exception);
        }
    }

    public static string NormalizePasskeyName(string? name)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? DefaultPasskeyName : name.Trim();
        return normalized.Length <= 80 ? normalized : normalized[..80];
    }

    public static PasskeyDeviceResponse ToDeviceResponse(PasskeyCredentialRecord credential) =>
        new(WebEncoders.Base64UrlEncode(credential.CredentialId), NormalizePasskeyName(credential.Name), credential.CreatedAtUtc);

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
public sealed record PasskeyManageOptionsRequest(string? Name);
public sealed record PasskeyManageVerifyRequest(JsonElement Credential);
public sealed record PasskeyBeginResult(string Flow, JsonElement Options);
public sealed record PasskeyDeviceResponse(string Id, string Name, DateTimeOffset CreatedAtUtc);

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
