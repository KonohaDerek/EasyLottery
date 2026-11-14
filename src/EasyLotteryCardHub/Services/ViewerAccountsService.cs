using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EasyLotteryCardHub.Services;

public interface IViewerCardRetirement
{
    Task RetireOwnedInstancesAsync(Guid viewerId, CancellationToken cancellationToken);
}

public sealed class NoOpViewerCardRetirement : IViewerCardRetirement
{
    public Task RetireOwnedInstancesAsync(Guid viewerId, CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class ViewerAccountsService(
    HubDbContext db,
    HubTokenService tokens,
    HubTokenSecurityOptions tokenOptions,
    TimeProvider timeProvider,
    IViewerEmailSender emailSender,
    ViewerCodeRequestRateLimiter rateLimiter,
    IViewerCardRetirement cardRetirement,
    IConfiguration configuration)
{
    private static readonly TimeSpan LoginCodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private const int MaxCodeAttempts = 5;

    private string CurrentTermsVersion => configuration["Viewer:TermsVersion"]?.Trim() is { Length: > 0 } version
        ? version
        : "1";

    public async Task RequestLoginCodeAsync(
        string? email,
        Guid? tenantId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeViewerEmail(email);
        var emailHash = ViewerSensitiveHasher.HashEmail(normalizedEmail, tokenOptions.SigningKey.Key);
        var ipHash = ViewerSensitiveHasher.HashIp(string.IsNullOrWhiteSpace(ipAddress) ? "unknown" : ipAddress, tokenOptions.SigningKey.Key);
        var now = timeProvider.GetUtcNow();
        string code;
        string? streamerName = null;

        await using (var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({emailHash}, 0))",
                cancellationToken);

            var record = await db.ViewerLoginCodes.SingleOrDefaultAsync(item => item.EmailHash == emailHash, cancellationToken);
            if (record is not null && record.CreatedAt + ResendCooldown > now)
            {
                var seconds = Math.Max(1, (int)Math.Ceiling((record.CreatedAt + ResendCooldown - now).TotalSeconds));
                throw new ViewerServiceException(StatusCodes.Status429TooManyRequests, "viewer_code_resend_too_soon", "viewer.code.resend.tooSoon", new { seconds });
            }

            if (!rateLimiter.TryAcquire(emailHash, ipHash, now, out var retryAfter))
            {
                var minutes = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalMinutes));
                throw new ViewerServiceException(StatusCodes.Status429TooManyRequests, "viewer_code_rate_limited", "viewer.code.error.rateLimited", new { minutes });
            }

            code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
            if (record is null)
            {
                record = new ViewerLoginCode
                {
                    Id = Guid.NewGuid(),
                    EmailHash = emailHash,
                    CodeHash = ViewerSensitiveHasher.HashCode(code, tokenOptions.SigningKey.Key),
                    CreatedAt = now,
                    ExpiresAt = now.Add(LoginCodeLifetime),
                    FailedAttempts = 0
                };
                db.ViewerLoginCodes.Add(record);
            }
            else
            {
                record.CodeHash = ViewerSensitiveHasher.HashCode(code, tokenOptions.SigningKey.Key);
                record.CreatedAt = now;
                record.ExpiresAt = now.Add(LoginCodeLifetime);
                record.FailedAttempts = 0;
                record.ConsumedAt = null;
            }

            if (tenantId is { } id)
            {
                streamerName = await db.Streamers.AsNoTracking()
                    .Where(item => item.Id == id && item.Status != TenantStatus.Deleted)
                    .Select(item => item.DisplayName)
                    .SingleOrDefaultAsync(cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        await emailSender.SendLoginCodeAsync(normalizedEmail, ViewerEmailCopyDeck.Create(code, streamerName), cancellationToken);
    }

    public async Task<ViewerSessionResponse> VerifyLoginCodeAsync(
        string? email,
        string? suppliedCode,
        string? ageBracket,
        bool termsAccepted,
        string? termsVersion,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeViewerEmail(email);
        var emailHash = ViewerSensitiveHasher.HashEmail(normalizedEmail, tokenOptions.SigningKey.Key);
        var now = timeProvider.GetUtcNow();

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var codeRecord = await db.ViewerLoginCodes
            .FromSqlInterpolated($"SELECT * FROM viewer_login_codes WHERE \"EmailHash\" = {emailHash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (codeRecord is null)
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_expired", "viewer.code.error.expired");
        if (codeRecord.FailedAttempts >= MaxCodeAttempts)
            throw new ViewerServiceException(StatusCodes.Status429TooManyRequests, "viewer_code_rate_limited", "viewer.code.error.rateLimited", new { minutes = 15 });
        if (codeRecord.ConsumedAt is not null || codeRecord.ExpiresAt <= now)
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_expired", "viewer.code.error.expired");

        var candidateHash = ViewerSensitiveHasher.HashCode(suppliedCode ?? string.Empty, tokenOptions.SigningKey.Key);
        if (!FixedTimeEqualsHex(candidateHash, codeRecord.CodeHash))
        {
            codeRecord.FailedAttempts++;
            if (codeRecord.FailedAttempts >= MaxCodeAttempts)
            {
                codeRecord.ConsumedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_too_many_attempts", "viewer.code.error.tooManyAttempts");
            }

            var remaining = MaxCodeAttempts - codeRecord.FailedAttempts;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_wrong", "viewer.code.error.wrongWithRemaining", new { remaining });
        }

        var viewer = await db.Viewers.SingleOrDefaultAsync(item => item.EmailNormalized == normalizedEmail, cancellationToken);
        if (viewer is null)
        {
            if (string.Equals(ageBracket?.Trim(), "under13", StringComparison.OrdinalIgnoreCase))
            {
                codeRecord.ConsumedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                throw new ViewerServiceException(StatusCodes.Status403Forbidden, "viewer_signup_under_13", "viewer.signup.under13.body");
            }

            if (ageBracket is not ("13_17" or "18_plus"))
                throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_age_required", "viewer.signup.age.required");
            if (!termsAccepted)
                throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_terms_required", "viewer.signup.terms.required");
            if (!string.Equals(termsVersion, CurrentTermsVersion, StringComparison.Ordinal))
                throw new ViewerServiceException(StatusCodes.Status409Conflict, "viewer_terms_updated", "viewer.signup.terms.updated");

            viewer = new Viewer
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                EmailNormalized = normalizedEmail,
                TermsVersionAccepted = CurrentTermsVersion,
                TermsAcceptedAt = now,
                AgeBracket = ageBracket,
                CreatedAt = now
            };
            db.Viewers.Add(viewer);
            HubAuditWriter.Append(db, timeProvider, "viewer.registered", "viewer", viewer.Id.ToString("D"), null,
                new { termsVersion = CurrentTermsVersion, ageBracket = viewer.AgeBracket });
        }
        else if (!string.Equals(viewer.TermsVersionAccepted, CurrentTermsVersion, StringComparison.Ordinal))
        {
            if (!termsAccepted || !string.Equals(termsVersion, CurrentTermsVersion, StringComparison.Ordinal))
                throw new ViewerServiceException(StatusCodes.Status409Conflict, "viewer_terms_updated", "viewer.signup.terms.updated");
            viewer.TermsVersionAccepted = CurrentTermsVersion;
            viewer.TermsAcceptedAt = now;
        }

        codeRecord.ConsumedAt = now;
        var session = CreateSession(viewer.Id, Guid.NewGuid(), now);
        db.ViewerRefreshTokens.Add(session.RefreshTokenRecord);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session.Response;
    }

    public async Task<ViewerSessionResponse> RotateRefreshTokenAsync(string? rawRefreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_invalid", "viewer.auth.refresh.invalid");

        var tokenHash = SecretHasher.Hash(rawRefreshToken);
        var now = timeProvider.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var current = await db.ViewerRefreshTokens
            .FromSqlInterpolated($"SELECT * FROM viewer_refresh_tokens WHERE \"TokenHash\" = {tokenHash} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
        if (current is null)
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_invalid", "viewer.auth.refresh.invalid");

        if (current.RevokedAt is not null)
        {
            await db.ViewerRefreshTokens
                .Where(item => item.SessionFamilyId == current.SessionFamilyId && item.RevokedAt == null)
                .ExecuteUpdateAsync(update => update.SetProperty(item => item.RevokedAt, now), cancellationToken);
            HubAuditWriter.Append(db, timeProvider, "viewer.refresh_reuse_detected", "viewer", current.ViewerId.ToString("D"), null,
                new { sessionFamilyId = current.SessionFamilyId });
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_reused", "viewer.auth.refresh.invalid");
        }

        if (current.ExpiresAt <= now)
        {
            current.RevokedAt = now;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_expired", "viewer.auth.refresh.invalid");
        }

        var viewer = await db.Viewers.SingleOrDefaultAsync(item => item.Id == current.ViewerId, cancellationToken);
        if (viewer is null)
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_invalid", "viewer.auth.refresh.invalid");

        current.RevokedAt = now;
        var session = CreateSession(viewer.Id, current.SessionFamilyId, now);
        current.ReplacedByTokenId = session.RefreshTokenRecord.Id;
        db.ViewerRefreshTokens.Add(session.RefreshTokenRecord);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session.Response;
    }

    public async Task<ViewerAccountExport> ExportAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        var viewer = await db.Viewers.AsNoTracking().SingleAsync(item => item.Id == viewerId, cancellationToken);
        var groups = await db.ViewerGroups.AsNoTracking()
            .Where(item => item.ViewerId == viewerId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var groupIds = groups.Select(item => item.Id).ToArray();
        var items = await db.ViewerGroupItems.AsNoTracking()
            .Where(item => groupIds.Contains(item.GroupId))
            .OrderBy(item => item.AddedAt)
            .ToListAsync(cancellationToken);
        var sessions = await db.ViewerRefreshTokens.AsNoTracking()
            .Where(item => item.ViewerId == viewerId)
            .OrderBy(item => item.CreatedAt)
            .Select(item => new ViewerExportSession(item.CreatedAt, item.ExpiresAt, item.RevokedAt))
            .ToListAsync(cancellationToken);

        HubAuditWriter.Append(db, timeProvider, "viewer.data_exported", "viewer", viewerId.ToString("D"), null,
            new { groupCount = groups.Count, sessionCount = sessions.Count });
        await db.SaveChangesAsync(cancellationToken);

        return new ViewerAccountExport(
            new ViewerExportProfile(viewer.Id, viewer.Email, viewer.TermsVersionAccepted, viewer.TermsAcceptedAt, viewer.AgeBracket, viewer.Handle, viewer.CreatedAt),
            groups.Select(group => new ViewerExportGroup(
                group.Id,
                group.Name,
                group.CreatedAt,
                items.Where(item => item.GroupId == group.Id).Select(item => item.CardInstanceId).ToArray())).ToArray(),
            sessions,
            timeProvider.GetUtcNow());
    }

    public async Task DeleteAccountAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var viewer = await db.Viewers.SingleOrDefaultAsync(item => item.Id == viewerId, cancellationToken);
        if (viewer is null)
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_account_unavailable", "viewer.auth.account.unavailable");

        await cardRetirement.RetireOwnedInstancesAsync(viewerId, cancellationToken);
        var emailHash = ViewerSensitiveHasher.HashEmail(viewer.EmailNormalized, tokenOptions.SigningKey.Key);
        await db.ViewerLoginCodes.Where(item => item.EmailHash == emailHash).ExecuteDeleteAsync(cancellationToken);
        HubAuditWriter.Append(db, timeProvider, "viewer.account_deleted", "viewer", viewerId.ToString("D"), null,
            new { groupsRemoved = await db.ViewerGroups.CountAsync(item => item.ViewerId == viewerId, cancellationToken) });
        db.Viewers.Remove(viewer);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private (ViewerSessionResponse Response, ViewerRefreshToken RefreshTokenRecord) CreateSession(Guid viewerId, Guid familyId, DateTimeOffset now)
    {
        var accessToken = tokens.IssueViewerToken(viewerId);
        var rawRefreshToken = SecretHasher.CreateViewerRefreshToken();
        var expiresAt = now.Add(RefreshTokenLifetime);
        var record = new ViewerRefreshToken
        {
            Id = Guid.NewGuid(),
            ViewerId = viewerId,
            SessionFamilyId = familyId,
            TokenHash = SecretHasher.Hash(rawRefreshToken),
            CreatedAt = now,
            ExpiresAt = expiresAt
        };
        return (
            new ViewerSessionResponse(viewerId, accessToken.AccessToken, accessToken.ExpiresAt, rawRefreshToken, expiresAt),
            record);
    }

    private string NormalizeViewerEmail(string? email)
    {
        try
        {
            return HubValidation.NormalizeEmail(email);
        }
        catch (HubServiceException)
        {
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "invalid_email", "viewer.auth.email.invalid");
        }
    }

    private static bool FixedTimeEqualsHex(string candidate, string expected)
    {
        try
        {
            var candidateBytes = Convert.FromHexString(candidate);
            var expectedBytes = Convert.FromHexString(expected);
            return candidateBytes.Length == expectedBytes.Length && CryptographicOperations.FixedTimeEquals(candidateBytes, expectedBytes);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record ViewerSessionResponse(
    Guid ViewerId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record ViewerAccountExport(
    ViewerExportProfile Viewer,
    IReadOnlyList<ViewerExportGroup> Groups,
    IReadOnlyList<ViewerExportSession> Sessions,
    DateTimeOffset ExportedAt);

public sealed record ViewerExportProfile(
    Guid Id,
    string Email,
    string TermsVersionAccepted,
    DateTimeOffset TermsAcceptedAt,
    string AgeBracket,
    string? Handle,
    DateTimeOffset CreatedAt);

public sealed record ViewerExportGroup(Guid Id, string Name, DateTimeOffset CreatedAt, IReadOnlyList<Guid> CardInstanceIds);
public sealed record ViewerExportSession(DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt);
