using System.Data;
using System.Globalization;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EasyLotteryCardHub.Data;
using EasyLotteryCardHub.Security;
using Npgsql;
using NpgsqlTypes;

namespace EasyLotteryCardHub.Viewers;

public sealed record ViewerCodeRequest(string Email, string AgeBracket, Guid? StreamerId);
public sealed record ViewerCodeRequestResult(string Status, int ExpiresInSeconds, int ResendAfterSeconds);
public sealed record ViewerCodeVerification(string Email, string Code, bool TermsAccepted, string? TermsVersion);
public sealed record ViewerGroup(Guid Id, Guid? StreamerId, string Name, DateTimeOffset CreatedAtUtc, IReadOnlyList<Guid> CardInstanceIds);
public sealed record ViewerAccountExport(Guid ViewerId, string? Handle, string AgeBracket, string TermsVersionAccepted,
    DateTimeOffset TermsAcceptedAtUtc, DateTimeOffset CreatedAtUtc, IReadOnlyList<ViewerGroup> Groups,
    IReadOnlyList<JsonElement> Collection, IReadOnlyList<JsonElement> Draws, IReadOnlyList<JsonElement> RedeemedCodes,
    IReadOnlyList<string> Omissions);

public sealed class ViewerAccountsService(
    HubDatabase database,
    IdentityHasher identityHasher,
    ViewerEmailOutbox emailOutbox,
    SiteAccessTokenIssuer tokens,
    TimeProvider timeProvider,
    IConfiguration configuration)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);
    private const int MaximumCodeAttempts = 5;
    private const int EmailRequestsPerWindow = 5;
    private const int IpRequestsPerWindow = 20;
    private NpgsqlDataSource DataSource => database.DataSource ?? throw new InvalidOperationException("Card Hub database is not configured.");

    public async Task<ViewerCodeRequestResult> RequestCodeAsync(ViewerCodeRequest request, string? remoteIp, CancellationToken cancellationToken)
    {
        if (string.Equals(request.AgeBracket?.Trim(), "under13", StringComparison.OrdinalIgnoreCase))
            throw new ViewerServiceException(StatusCodes.Status403Forbidden, "viewer_signup_under_13", "viewer.signup.under13.body");
        var ageBracket = NormalizeAgeBracket(request.AgeBracket);
        var email = NormalizeEmail(request.Email);
        _ = CurrentTermsVersion;
        var emailHash = identityHasher.HashEmail(email);
        var emailBucketHash = identityHasher.HashPurpose("viewer-rate-email", Convert.ToHexString(emailHash));
        var normalizedIp = string.IsNullOrWhiteSpace(remoteIp) ? "unknown" : remoteIp.Trim();
        var ipBucketHash = identityHasher.HashPurpose("viewer-rate-ip", normalizedIp);
        var now = timeProvider.GetUtcNow();
        string? streamerName = null;
        if (request.StreamerId is { } streamerId)
        {
            await using var lookupConnection = await DataSource.OpenConnectionAsync(cancellationToken);
            await using var lookup = new NpgsqlCommand("SELECT display_name FROM streamers WHERE id = @id AND status <> 'deleted'", lookupConnection);
            lookup.Parameters.AddWithValue("id", streamerId);
            streamerName = (string?)await lookup.ExecuteScalarAsync(cancellationToken);
            if (streamerName is null)
                throw new ViewerServiceException(StatusCodes.Status404NotFound, "viewer_streamer_not_found", "viewer.auth.streamer.notFound");
        }

        ViewerServiceException? rejection = null;
        await using (var connection = await DataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken))
        {
            var advisoryKey = BitConverter.ToInt64(emailHash, 0);
            await using (var advisory = new NpgsqlCommand("SELECT pg_advisory_xact_lock(@key)", connection, transaction))
            {
                advisory.Parameters.AddWithValue("key", advisoryKey);
                await advisory.ExecuteNonQueryAsync(cancellationToken);
            }

            var emailCount = await IncrementRateBucketAsync(connection, transaction, emailBucketHash, "email", now, cancellationToken);
            var ipCount = await IncrementRateBucketAsync(connection, transaction, ipBucketHash, "ip", now, cancellationToken);
            await using (var cleanupBuckets = new NpgsqlCommand("DELETE FROM viewer_login_rate_limits WHERE window_started_at_utc < @older_than", connection, transaction))
            {
                cleanupBuckets.Parameters.AddWithValue("older_than", now.Subtract(TimeSpan.FromDays(1)));
                await cleanupBuckets.ExecuteNonQueryAsync(cancellationToken);
            }
            var retryAt = await LatestRequestAtAsync(connection, transaction, emailHash, cancellationToken);
            if (retryAt is { } latest && latest + ResendCooldown > now)
            {
                var seconds = Math.Max(1, (int)Math.Ceiling((latest + ResendCooldown - now).TotalSeconds));
                rejection = new ViewerServiceException(StatusCodes.Status429TooManyRequests, "viewer_code_resend_too_soon", "viewer.code.resend.tooSoon", new { seconds });
            }
            else if (emailCount > EmailRequestsPerWindow || ipCount > IpRequestsPerWindow)
            {
                var retryMinutes = Math.Max(1, (int)Math.Ceiling(RateWindow.TotalMinutes));
                rejection = new ViewerServiceException(StatusCodes.Status429TooManyRequests, "viewer_code_rate_limited", "viewer.code.error.rateLimited", new { minutes = retryMinutes });
            }

            if (rejection is null)
            {
                await using (var invalidate = new NpgsqlCommand("UPDATE viewer_login_codes SET consumed_at_utc = @now WHERE email_hash = @email_hash AND consumed_at_utc IS NULL", connection, transaction))
                {
                    invalidate.Parameters.AddWithValue("now", now);
                    invalidate.Parameters.AddWithValue("email_hash", emailHash);
                    await invalidate.ExecuteNonQueryAsync(cancellationToken);
                }
                await using (var cancel = new NpgsqlCommand("UPDATE viewer_email_outbox SET cancelled_at_utc = @now WHERE cancelled_at_utc IS NULL AND delivered_at_utc IS NULL AND login_code_id IN (SELECT id FROM viewer_login_codes WHERE email_hash = @email_hash AND consumed_at_utc = @now)", connection, transaction))
                {
                    cancel.Parameters.AddWithValue("now", now);
                    cancel.Parameters.AddWithValue("email_hash", emailHash);
                    await cancel.ExecuteNonQueryAsync(cancellationToken);
                }

                var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
                var loginCodeId = Guid.NewGuid();
                var expiresAt = now.Add(CodeLifetime);
                var codeHash = identityHasher.HashPurpose("viewer-login-code", $"{Convert.ToHexString(emailHash)}:{code}");
                await using (var insert = new NpgsqlCommand("INSERT INTO viewer_login_codes (id, email_hash, code_hash, expires_at_utc, attempt_count, requested_at_utc, consumed_at_utc, age_bracket, streamer_id) VALUES (@id, @email_hash, @code_hash, @expires, 0, @requested, NULL, @age, @streamer)", connection, transaction))
                {
                    insert.Parameters.AddWithValue("id", loginCodeId);
                    insert.Parameters.AddWithValue("email_hash", emailHash);
                    insert.Parameters.AddWithValue("code_hash", codeHash);
                    insert.Parameters.AddWithValue("expires", expiresAt);
                    insert.Parameters.AddWithValue("requested", now);
                    insert.Parameters.AddWithValue("age", ageBracket);
                    insert.Parameters.Add(new NpgsqlParameter("streamer", NpgsqlDbType.Uuid) { Value = (object?)request.StreamerId ?? DBNull.Value });
                    await insert.ExecuteNonQueryAsync(cancellationToken);
                }
                var emailMessage = CreateLoginEmail(email, code, expiresAt, streamerName);
                await emailOutbox.EnqueueAsync(loginCodeId, emailMessage, connection, transaction, cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        }
        if (rejection is not null) throw rejection;
        return new ViewerCodeRequestResult("queued", (int)CodeLifetime.TotalSeconds, (int)ResendCooldown.TotalSeconds);
    }

    public async Task<ViewerSession> VerifyCodeAsync(ViewerCodeVerification request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var emailHash = identityHasher.HashEmail(email);
        var now = timeProvider.GetUtcNow();
        var currentTermsVersion = CurrentTermsVersion;
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        Guid codeId;
        byte[] expectedHash;
        DateTimeOffset expiresAt;
        int attempts;
        string ageBracket;
        await using (var findCode = new NpgsqlCommand("SELECT id, code_hash, expires_at_utc, attempt_count, age_bracket FROM viewer_login_codes WHERE email_hash = @email_hash ORDER BY requested_at_utc DESC LIMIT 1 FOR UPDATE", connection, transaction))
        {
            findCode.Parameters.AddWithValue("email_hash", emailHash);
            await using var reader = await findCode.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_expired", "viewer.code.error.expired");
            codeId = reader.GetGuid(0);
            expectedHash = (byte[])reader[1];
            expiresAt = reader.GetFieldValue<DateTimeOffset>(2);
            attempts = reader.GetInt32(3);
            ageBracket = reader.IsDBNull(4) ? "" : reader.GetString(4);
        }

        if (attempts >= MaximumCodeAttempts)
            throw new ViewerServiceException(StatusCodes.Status429TooManyRequests, "viewer_code_rate_limited", "viewer.code.error.rateLimited", new { minutes = 15 });
        if (expiresAt <= now)
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_expired", "viewer.code.error.expired");

        var candidateHash = identityHasher.HashPurpose("viewer-login-code", $"{Convert.ToHexString(emailHash)}:{request.Code ?? ""}");
        if (!CryptographicOperations.FixedTimeEquals(expectedHash, candidateHash))
        {
            attempts++;
            await using var wrong = new NpgsqlCommand("UPDATE viewer_login_codes SET attempt_count = @attempts, consumed_at_utc = CASE WHEN @attempts >= @max THEN @now ELSE consumed_at_utc END WHERE id = @id", connection, transaction);
            wrong.Parameters.AddWithValue("attempts", attempts);
            wrong.Parameters.AddWithValue("max", MaximumCodeAttempts);
            wrong.Parameters.AddWithValue("now", now);
            wrong.Parameters.AddWithValue("id", codeId);
            await wrong.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (attempts >= MaximumCodeAttempts)
                throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_too_many_attempts", "viewer.code.error.tooManyAttempts");
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_wrong", "viewer.code.error.wrongWithRemaining", new { remaining = MaximumCodeAttempts - attempts });
        }

        if (ageBracket is not ("13_17" or "18_plus"))
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_age_required", "viewer.signup.age.required");
        if (request.TermsAccepted && !string.Equals(request.TermsVersion, currentTermsVersion, StringComparison.Ordinal))
            throw new ViewerServiceException(StatusCodes.Status409Conflict, "viewer_terms_updated", "viewer.signup.terms.updated");

        Guid viewerId;
        string acceptedTermsVersion;
        await using (var findViewer = new NpgsqlCommand("SELECT id, terms_version_accepted FROM viewers WHERE email_hash = @email_hash AND deleted_at_utc IS NULL FOR UPDATE", connection, transaction))
        {
            findViewer.Parameters.AddWithValue("email_hash", emailHash);
            await using var reader = await findViewer.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                viewerId = reader.GetGuid(0);
                acceptedTermsVersion = reader.GetString(1);
            }
            else
            {
                viewerId = Guid.NewGuid();
                acceptedTermsVersion = "";
            }
        }

        if (acceptedTermsVersion.Length == 0)
        {
            if (!request.TermsAccepted)
                throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_terms_required", "viewer.signup.terms.required");
            await using var insertViewer = new NpgsqlCommand("INSERT INTO viewers (id, email_hash, handle, terms_version_accepted, terms_accepted_at_utc, age_bracket) VALUES (@id, @email_hash, NULL, @terms_version, @terms_at, @age)", connection, transaction);
            insertViewer.Parameters.AddWithValue("id", viewerId);
            insertViewer.Parameters.AddWithValue("email_hash", emailHash);
            insertViewer.Parameters.AddWithValue("terms_version", currentTermsVersion);
            insertViewer.Parameters.AddWithValue("terms_at", now);
            insertViewer.Parameters.AddWithValue("age", ageBracket);
            await insertViewer.ExecuteNonQueryAsync(cancellationToken);
            await WriteAuditAsync(connection, transaction, "viewer.registered", "viewer", viewerId.ToString("D"), null,
                new { termsVersion = currentTermsVersion, ageBracket }, cancellationToken);
        }
        else
        {
            if (!string.Equals(acceptedTermsVersion, currentTermsVersion, StringComparison.Ordinal) && !request.TermsAccepted)
                throw new ViewerServiceException(StatusCodes.Status409Conflict, "viewer_terms_updated", "viewer.signup.terms.updated");
            await using var updateViewer = new NpgsqlCommand("UPDATE viewers SET age_bracket = @age, terms_version_accepted = CASE WHEN @accepted THEN @terms_version ELSE terms_version_accepted END, terms_accepted_at_utc = CASE WHEN @accepted THEN @now ELSE terms_accepted_at_utc END WHERE id = @id", connection, transaction);
            updateViewer.Parameters.AddWithValue("age", ageBracket);
            updateViewer.Parameters.AddWithValue("accepted", request.TermsAccepted);
            updateViewer.Parameters.AddWithValue("terms_version", currentTermsVersion);
            updateViewer.Parameters.AddWithValue("now", now);
            updateViewer.Parameters.AddWithValue("id", viewerId);
            await updateViewer.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var consume = new NpgsqlCommand("UPDATE viewer_login_codes SET consumed_at_utc = @now WHERE id = @id AND consumed_at_utc IS NULL", connection, transaction))
        {
            consume.Parameters.AddWithValue("now", now);
            consume.Parameters.AddWithValue("id", codeId);
            if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_code_expired", "viewer.code.error.expired");
        }
        var session = CreateSession(viewerId, ageBracket, Guid.NewGuid(), now);
        await using (var insertRefresh = new NpgsqlCommand("INSERT INTO viewer_refresh_tokens (id, viewer_id, session_family_id, token_hash, created_at_utc, expires_at_utc) VALUES (@id, @viewer, @family, @hash, @created, @expires)", connection, transaction))
        {
            AddRefreshTokenParameters(insertRefresh, session, now);
            await insertRefresh.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return session.Response;
    }

    public async Task<ViewerSession> RefreshAsync(string? rawRefreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_invalid", "viewer.auth.refresh.invalid");
        var tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(rawRefreshToken));
        var now = timeProvider.GetUtcNow();
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        Guid tokenId;
        Guid viewerId;
        Guid familyId;
        DateTimeOffset expiresAt;
        DateTimeOffset? revokedAt;
        await using (var lookup = new NpgsqlCommand("SELECT id, viewer_id, session_family_id, expires_at_utc, revoked_at_utc FROM viewer_refresh_tokens WHERE token_hash = @hash FOR UPDATE", connection, transaction))
        {
            lookup.Parameters.AddWithValue("hash", tokenHash);
            await using var reader = await lookup.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_invalid", "viewer.auth.refresh.invalid");
            tokenId = reader.GetGuid(0);
            viewerId = reader.GetGuid(1);
            familyId = reader.GetGuid(2);
            expiresAt = reader.GetFieldValue<DateTimeOffset>(3);
            revokedAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4);
        }
        if (revokedAt is not null)
        {
            await using var revokeFamily = new NpgsqlCommand("UPDATE viewer_refresh_tokens SET revoked_at_utc = COALESCE(revoked_at_utc, @now) WHERE session_family_id = @family", connection, transaction);
            revokeFamily.Parameters.AddWithValue("now", now);
            revokeFamily.Parameters.AddWithValue("family", familyId);
            await revokeFamily.ExecuteNonQueryAsync(cancellationToken);
            await WriteAuditAsync(connection, transaction, "viewer.refresh_reuse_detected", "viewer", viewerId.ToString("D"), null,
                new { sessionFamilyId = familyId }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_reused", "viewer.auth.refresh.invalid");
        }
        if (expiresAt <= now)
        {
            await using var expire = new NpgsqlCommand("UPDATE viewer_refresh_tokens SET revoked_at_utc = @now WHERE id = @id", connection, transaction);
            expire.Parameters.AddWithValue("now", now);
            expire.Parameters.AddWithValue("id", tokenId);
            await expire.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_expired", "viewer.auth.refresh.invalid");
        }

        string ageBracket;
        await using (var findViewer = new NpgsqlCommand("SELECT age_bracket FROM viewers WHERE id = @id AND deleted_at_utc IS NULL FOR UPDATE", connection, transaction))
        {
            findViewer.Parameters.AddWithValue("id", viewerId);
            ageBracket = (string?)await findViewer.ExecuteScalarAsync(cancellationToken) ?? "";
        }
        if (ageBracket is not ("13_17" or "18_plus"))
            throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_invalid", "viewer.auth.refresh.invalid");

        var next = CreateSession(viewerId, ageBracket, familyId, now);
        await using (var revoke = new NpgsqlCommand("UPDATE viewer_refresh_tokens SET revoked_at_utc = @now, replaced_by_token_id = @replacement WHERE id = @id AND revoked_at_utc IS NULL", connection, transaction))
        {
            revoke.Parameters.AddWithValue("now", now);
            revoke.Parameters.AddWithValue("replacement", next.RefreshTokenId);
            revoke.Parameters.AddWithValue("id", tokenId);
            if (await revoke.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_refresh_invalid", "viewer.auth.refresh.invalid");
        }
        await using (var insert = new NpgsqlCommand("INSERT INTO viewer_refresh_tokens (id, viewer_id, session_family_id, token_hash, created_at_utc, expires_at_utc) VALUES (@id, @viewer, @family, @hash, @created, @expires)", connection, transaction))
        {
            AddRefreshTokenParameters(insert, next, now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return next.Response;
    }

    public async Task<string> SetHandleAsync(Guid viewerId, string? rawHandle, CancellationToken cancellationToken)
    {
        var handle = string.IsNullOrWhiteSpace(rawHandle) ? null : rawHandle.Trim();
        if (!ViewerPolicy.IsValidHandle(handle))
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_handle_invalid", "viewer.profile.handle.invalid");
        if (ViewerPolicy.IsReservedHandle(handle))
            throw new ViewerServiceException(StatusCodes.Status409Conflict, "viewer_handle_reserved", "viewer.profile.handle.reserved");
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var update = new NpgsqlCommand("UPDATE viewers SET handle = @handle WHERE id = @id AND deleted_at_utc IS NULL", connection, transaction);
            update.Parameters.Add(new NpgsqlParameter("handle", NpgsqlDbType.Varchar) { Value = (object?)handle ?? DBNull.Value });
            update.Parameters.AddWithValue("id", viewerId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new ViewerServiceException(StatusCodes.Status401Unauthorized, "viewer_account_unavailable", "viewer.auth.session.invalid");
            await WriteAuditAsync(connection, transaction, "viewer.handle_changed", "viewer", viewerId.ToString("D"), null, new { handle }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return handle ?? "";
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ViewerServiceException(StatusCodes.Status409Conflict, "viewer_handle_taken", "viewer.profile.handle.taken");
        }
    }

    public async Task<bool> IsActiveAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM viewers WHERE id = @viewer AND deleted_at_utc IS NULL)", connection);
        command.Parameters.AddWithValue("viewer", viewerId);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    public async Task<IReadOnlyList<ViewerGroup>> ListGroupsAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        var groups = new List<ViewerGroup>();
        await using (var command = new NpgsqlCommand("SELECT id, streamer_id, name, created_at_utc FROM viewer_groups WHERE viewer_id = @viewer ORDER BY created_at_utc", connection))
        {
            command.Parameters.AddWithValue("viewer", viewerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                groups.Add(new ViewerGroup(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), []));
        }
        foreach (var group in groups)
        {
            await using var items = new NpgsqlCommand("SELECT card_instance_id FROM viewer_group_items WHERE group_id = @group ORDER BY created_at_utc", connection);
            items.Parameters.AddWithValue("group", group.Id);
            await using var reader = await items.ExecuteReaderAsync(cancellationToken);
            var ids = new List<Guid>();
            while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));
            groups[groups.IndexOf(group)] = group with { CardInstanceIds = ids };
        }
        return groups;
    }

    public async Task<ViewerGroup> CreateGroupAsync(Guid viewerId, Guid? streamerId, string? rawName, CancellationToken cancellationToken)
    {
        var name = rawName?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_group_name_invalid", "viewer.groups.name.invalid");
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (streamerId is { } tenantId)
        {
            await using var tenant = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM streamers WHERE id = @id AND status = 'active')", connection, transaction);
            tenant.Parameters.AddWithValue("id", tenantId);
            if (!(bool)(await tenant.ExecuteScalarAsync(cancellationToken) ?? false))
                throw new ViewerServiceException(StatusCodes.Status404NotFound, "viewer_group_streamer_not_found", "viewer.groups.streamer.notFound");
        }
        var groupId = Guid.NewGuid();
        try
        {
            await using var insert = new NpgsqlCommand("INSERT INTO viewer_groups (id, viewer_id, streamer_id, name) VALUES (@id, @viewer, @streamer, @name)", connection, transaction);
            insert.Parameters.AddWithValue("id", groupId);
            insert.Parameters.AddWithValue("viewer", viewerId);
            insert.Parameters.Add(new NpgsqlParameter("streamer", NpgsqlDbType.Uuid) { Value = (object?)streamerId ?? DBNull.Value });
            insert.Parameters.AddWithValue("name", name);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await WriteAuditAsync(connection, transaction, "viewer.group_created", "viewer", viewerId.ToString("D"), streamerId, new { groupId, name }, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ViewerGroup(groupId, streamerId, name, timeProvider.GetUtcNow(), []);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ViewerServiceException(StatusCodes.Status409Conflict, "viewer_group_name_taken", "viewer.groups.name.taken");
        }
    }

    public async Task AddCardToGroupAsync(Guid viewerId, Guid groupId, Guid cardInstanceId, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await RequireOwnedCardAsync(connection, transaction, viewerId, cardInstanceId, cancellationToken);
        await using (var ownsGroup = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM viewer_groups WHERE id = @group AND viewer_id = @viewer)", connection, transaction))
        {
            ownsGroup.Parameters.AddWithValue("group", groupId);
            ownsGroup.Parameters.AddWithValue("viewer", viewerId);
            if (!(bool)(await ownsGroup.ExecuteScalarAsync(cancellationToken) ?? false))
                throw new ViewerServiceException(StatusCodes.Status404NotFound, "viewer_group_not_found", "viewer.groups.notFound");
        }
        await using var insert = new NpgsqlCommand("INSERT INTO viewer_group_items (group_id, card_instance_id) SELECT id, @card FROM viewer_groups WHERE id = @group AND viewer_id = @viewer ON CONFLICT DO NOTHING", connection, transaction);
        insert.Parameters.AddWithValue("card", cardInstanceId);
        insert.Parameters.AddWithValue("group", groupId);
        insert.Parameters.AddWithValue("viewer", viewerId);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RemoveCardFromGroupAsync(Guid viewerId, Guid groupId, Guid cardInstanceId, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("DELETE FROM viewer_group_items i USING viewer_groups g WHERE i.group_id = g.id AND g.viewer_id = @viewer AND g.id = @group AND i.card_instance_id = @card", connection);
        command.Parameters.AddWithValue("viewer", viewerId);
        command.Parameters.AddWithValue("group", groupId);
        command.Parameters.AddWithValue("card", cardInstanceId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new ViewerServiceException(StatusCodes.Status404NotFound, "viewer_group_item_not_found", "viewer.groups.item.notFound");
    }

    public async Task DeleteGroupAsync(Guid viewerId, Guid groupId, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("DELETE FROM viewer_groups WHERE id = @group AND viewer_id = @viewer", connection);
        command.Parameters.AddWithValue("group", groupId);
        command.Parameters.AddWithValue("viewer", viewerId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new ViewerServiceException(StatusCodes.Status404NotFound, "viewer_group_not_found", "viewer.groups.notFound");
    }

    public async Task DeleteAccountAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        byte[] emailHash;
        await using (var find = new NpgsqlCommand("SELECT email_hash FROM viewers WHERE id = @id AND deleted_at_utc IS NULL FOR UPDATE", connection, transaction))
        {
            find.Parameters.AddWithValue("id", viewerId);
            var value = (byte[]?)await find.ExecuteScalarAsync(cancellationToken);
            if (value is null) throw new ViewerServiceException(StatusCodes.Status404NotFound, "viewer_account_not_found", "viewer.account.notFound");
            emailHash = value;
        }
        if (await TableExistsAsync(connection, transaction, "card_instances", cancellationToken))
        {
            await using var retire = new NpgsqlCommand("UPDATE card_instances SET owner_viewer_id = NULL, status = 'retired' WHERE owner_viewer_id = @viewer", connection, transaction);
            retire.Parameters.AddWithValue("viewer", viewerId);
            await retire.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var removeOutbox = new NpgsqlCommand("DELETE FROM viewer_email_outbox WHERE login_code_id IN (SELECT id FROM viewer_login_codes WHERE email_hash = @email_hash)", connection, transaction))
        {
            removeOutbox.Parameters.AddWithValue("email_hash", emailHash);
            await removeOutbox.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var removeCodes = new NpgsqlCommand("DELETE FROM viewer_login_codes WHERE email_hash = @email_hash", connection, transaction))
        {
            removeCodes.Parameters.AddWithValue("email_hash", emailHash);
            await removeCodes.ExecuteNonQueryAsync(cancellationToken);
        }
        var rateKey = identityHasher.HashPurpose("viewer-rate-email", Convert.ToHexString(emailHash));
        await using (var removeRate = new NpgsqlCommand("DELETE FROM viewer_login_rate_limits WHERE bucket_hash = @hash", connection, transaction))
        {
            removeRate.Parameters.AddWithValue("hash", rateKey);
            await removeRate.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var removeGroups = new NpgsqlCommand("DELETE FROM viewer_groups WHERE viewer_id = @viewer", connection, transaction))
        {
            removeGroups.Parameters.AddWithValue("viewer", viewerId);
            await removeGroups.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var removeSessions = new NpgsqlCommand("DELETE FROM viewer_refresh_tokens WHERE viewer_id = @viewer", connection, transaction))
        {
            removeSessions.Parameters.AddWithValue("viewer", viewerId);
            await removeSessions.ExecuteNonQueryAsync(cancellationToken);
        }
        var anonymizedHash = identityHasher.HashPurpose("deleted-viewer", Guid.NewGuid().ToString("N"));
        await using (var anonymize = new NpgsqlCommand("UPDATE viewers SET email_hash = @email_hash, handle = NULL, deleted_at_utc = @now WHERE id = @viewer", connection, transaction))
        {
            anonymize.Parameters.AddWithValue("email_hash", anonymizedHash);
            anonymize.Parameters.AddWithValue("now", now);
            anonymize.Parameters.AddWithValue("viewer", viewerId);
            await anonymize.ExecuteNonQueryAsync(cancellationToken);
        }
        await WriteAuditAsync(connection, transaction, "viewer.account_deleted", "viewer", viewerId.ToString("D"), null,
            new { deletedAtUtc = now }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ViewerAccountExport> ExportAsync(Guid viewerId, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        Guid id;
        string? handle;
        string ageBracket;
        string termsVersion;
        DateTimeOffset termsAcceptedAt;
        DateTimeOffset createdAt;
        await using (var account = new NpgsqlCommand("SELECT id, handle, age_bracket, terms_version_accepted, terms_accepted_at_utc, created_at_utc FROM viewers WHERE id = @viewer AND deleted_at_utc IS NULL", connection, transaction))
        {
            account.Parameters.AddWithValue("viewer", viewerId);
            await using var reader = await account.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new ViewerServiceException(StatusCodes.Status404NotFound, "viewer_account_not_found", "viewer.account.notFound");
            id = reader.GetGuid(0);
            handle = reader.IsDBNull(1) ? null : reader.GetString(1);
            ageBracket = reader.GetString(2);
            termsVersion = reader.GetString(3);
            termsAcceptedAt = reader.GetFieldValue<DateTimeOffset>(4);
            createdAt = reader.GetFieldValue<DateTimeOffset>(5);
        }
        var groups = await ListGroupsAsync(viewerId, connection, transaction, cancellationToken);
        var collection = new List<JsonElement>();
        var draws = new List<JsonElement>();
        var redeemedCodes = new List<JsonElement>();
        var omissions = new List<string>();
        if (await TableExistsAsync(connection, transaction, "card_instances", cancellationToken))
            collection.AddRange(await ReadJsonRowsAsync(connection, transaction,
                "SELECT jsonb_build_object('id', id, 'cardId', card_id, 'status', status, 'origin', origin)::text FROM card_instances WHERE owner_viewer_id = @viewer ORDER BY id", viewerId, cancellationToken));
        else omissions.Add("collection (card_instances table is introduced in the catalog/draw wave)");
        if (await TableExistsAsync(connection, transaction, "pack_draws", cancellationToken))
            draws.AddRange(await ReadJsonRowsAsync(connection, transaction,
                "SELECT to_jsonb(d)::text FROM pack_draws d JOIN card_instances c ON c.id = d.instance_id WHERE c.owner_viewer_id = @viewer ORDER BY d.drawn_at", viewerId, cancellationToken));
        else omissions.Add("draw history (pack_draws table is introduced in the draw wave)");
        if (await TableExistsAsync(connection, transaction, "redemption_codes", cancellationToken))
            redeemedCodes.AddRange(await ReadJsonRowsAsync(connection, transaction,
                "SELECT to_jsonb(r)::text FROM redemption_codes r WHERE r.redeemed_by_viewer_id = @viewer ORDER BY r.redeemed_at_utc", viewerId, cancellationToken));
        else omissions.Add("redeemed code history (redemption_codes table is introduced in the redemption wave)");

        omissions.Add("email address (the account stores only a non-reversible keyed identifier)");
        var payload = new ViewerAccountExport(id, handle, ageBracket, termsVersion, termsAcceptedAt, createdAt,
            groups, collection, draws, redeemedCodes, omissions);
        await WriteAuditAsync(connection, transaction, "viewer.data_exported", "viewer", viewerId.ToString("D"), null,
            new { exportedAtUtc = timeProvider.GetUtcNow() }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return payload;
    }

    private async Task<IReadOnlyList<ViewerGroup>> ListGroupsAsync(Guid viewerId, NpgsqlConnection connection, CancellationToken cancellationToken)
        => await ListGroupsAsync(viewerId, connection, null, cancellationToken);

    private async Task<IReadOnlyList<ViewerGroup>> ListGroupsAsync(Guid viewerId, NpgsqlConnection connection, NpgsqlTransaction? transaction, CancellationToken cancellationToken)
    {
        var groups = new List<ViewerGroup>();
        await using (var command = new NpgsqlCommand("SELECT id, streamer_id, name, created_at_utc FROM viewer_groups WHERE viewer_id = @viewer ORDER BY created_at_utc", connection, transaction))
        {
            command.Parameters.AddWithValue("viewer", viewerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                groups.Add(new ViewerGroup(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3), []));
        }
        for (var i = 0; i < groups.Count; i++)
        {
            await using var items = new NpgsqlCommand("SELECT card_instance_id FROM viewer_group_items WHERE group_id = @group ORDER BY created_at_utc", connection, transaction);
            items.Parameters.AddWithValue("group", groups[i].Id);
            await using var reader = await items.ExecuteReaderAsync(cancellationToken);
            var ids = new List<Guid>();
            while (await reader.ReadAsync(cancellationToken)) ids.Add(reader.GetGuid(0));
            groups[i] = groups[i] with { CardInstanceIds = ids };
        }
        return groups;
    }

    private static async Task<IReadOnlyList<JsonElement>> ReadJsonRowsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string sql, Guid viewerId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("viewer", viewerId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<JsonElement>();
        while (await reader.ReadAsync(cancellationToken))
        {
            using var document = JsonDocument.Parse(reader.GetString(0));
            rows.Add(document.RootElement.Clone());
        }
        return rows;
    }

    private static async Task RequireOwnedCardAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        Guid viewerId, Guid cardId, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, transaction, "card_instances", cancellationToken))
            throw new ViewerServiceException(StatusCodes.Status409Conflict, "viewer_collection_not_available", "viewer.collection.notAvailable");
        await using var owns = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM card_instances WHERE id = @card AND owner_viewer_id = @viewer AND status = 'owned')", connection, transaction);
        owns.Parameters.AddWithValue("card", cardId);
        owns.Parameters.AddWithValue("viewer", viewerId);
        if (!(bool)(await owns.ExecuteScalarAsync(cancellationToken) ?? false))
            throw new ViewerServiceException(StatusCodes.Status404NotFound, "viewer_card_not_found", "viewer.collection.cardNotFound");
    }

    private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction,
        string name, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", connection, transaction);
        command.Parameters.AddWithValue("name", name);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private async Task<int> IncrementRateBucketAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        byte[] bucketHash, string bucketType, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("INSERT INTO viewer_login_rate_limits (bucket_hash, bucket_type, window_started_at_utc, request_count) VALUES (@hash, @type, @now, 1) ON CONFLICT (bucket_hash) DO UPDATE SET window_started_at_utc = CASE WHEN viewer_login_rate_limits.window_started_at_utc <= @reset_before THEN @now ELSE viewer_login_rate_limits.window_started_at_utc END, request_count = CASE WHEN viewer_login_rate_limits.window_started_at_utc <= @reset_before THEN 1 ELSE viewer_login_rate_limits.request_count + 1 END RETURNING request_count", connection, transaction);
        command.Parameters.AddWithValue("hash", bucketHash);
        command.Parameters.AddWithValue("type", bucketType);
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("reset_before", now.Subtract(RateWindow));
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task<DateTimeOffset?> LatestRequestAtAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        byte[] emailHash, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT max(requested_at_utc) FROM viewer_login_codes WHERE email_hash = @email_hash", connection, transaction);
        command.Parameters.AddWithValue("email_hash", emailHash);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is DateTimeOffset timestamp ? timestamp : null;
    }

    private async Task WriteAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string eventType, string actorType, string actorId, Guid? tenantId, object payload, CancellationToken cancellationToken)
    {
        await using var audit = new NpgsqlCommand("INSERT INTO audit_events (id, tenant_id, actor_type, actor_id, event_type, subject_type, subject_id, payload) VALUES (@id, @tenant, @actor_type, @actor_id, @event_type, 'viewer', @subject, @payload::jsonb)", connection, transaction);
        audit.Parameters.AddWithValue("id", Guid.NewGuid());
        audit.Parameters.Add(new NpgsqlParameter("tenant", NpgsqlDbType.Uuid) { Value = (object?)tenantId ?? DBNull.Value });
        audit.Parameters.AddWithValue("actor_type", actorType);
        audit.Parameters.AddWithValue("actor_id", actorId);
        audit.Parameters.AddWithValue("event_type", eventType);
        audit.Parameters.AddWithValue("subject", actorId);
        audit.Parameters.AddWithValue("payload", JsonSerializer.Serialize(payload));
        await audit.ExecuteNonQueryAsync(cancellationToken);
    }

    private StoredViewerSession CreateSession(Guid viewerId, string ageBracket, Guid familyId, DateTimeOffset now)
    {
        var (accessToken, expiresIn) = tokens.IssueViewer(viewerId, ageBracket);
        var refreshToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return new StoredViewerSession(new ViewerSession(viewerId, ageBracket, accessToken, refreshToken, expiresIn),
            Guid.NewGuid(), SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)), familyId, now.Add(RefreshLifetime));
    }

    private static void AddRefreshTokenParameters(NpgsqlCommand command, StoredViewerSession session, DateTimeOffset now)
    {
        command.Parameters.AddWithValue("id", session.RefreshTokenId);
        command.Parameters.AddWithValue("viewer", session.Response.ViewerId);
        command.Parameters.AddWithValue("family", session.SessionFamilyId);
        command.Parameters.AddWithValue("hash", session.RefreshTokenHash);
        command.Parameters.AddWithValue("created", now);
        command.Parameters.AddWithValue("expires", session.RefreshExpiresAtUtc);
    }

    private static ViewerLoginEmail CreateLoginEmail(string email, string code, DateTimeOffset expiresAt, string? streamerName)
    {
        var subject = streamerName is null
            ? $"你的 Card Hub 登入驗證碼：{code}"
            : $"{streamerName} 卡牌收藏：你的登入驗證碼 {code}";
        var intro = streamerName is null ? "你正在登入 Card Hub。" : $"你正在從 {streamerName} 的卡牌活動登入 Card Hub。";
        var body = string.Join("\n\n", intro, $"你的驗證碼是：{code}", "驗證碼 10 分鐘內有效，只能使用一次。",
            "如果不是你本人操作請忽略這封信，你的帳號不會有任何變更。請勿將驗證碼提供給任何人，包括實況主或客服人員。", "EasyLottery Card Hub", "這是系統自動寄出的信件，請勿直接回覆。");
        return new ViewerLoginEmail(email, subject, body, code, expiresAt);
    }

    private string CurrentTermsVersion => configuration["CardHub:Viewer:TermsVersion"]?.Trim() is { Length: > 0 } version
        ? version
        : throw new ViewerServiceException(StatusCodes.Status503ServiceUnavailable, "viewer_terms_unavailable", "viewer.signup.terms.unavailable");

    private static string NormalizeAgeBracket(string? ageBracket) => ageBracket?.Trim() switch
    {
        "13_17" => "13_17",
        "18_plus" => "18_plus",
        _ => throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_age_required", "viewer.signup.age.required")
    };

    private static string NormalizeEmail(string? email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length > 254 || !MailAddress.TryCreate(normalized, out var address)
            || !string.Equals(address.Address, normalized, StringComparison.Ordinal))
            throw new ViewerServiceException(StatusCodes.Status400BadRequest, "viewer_email_invalid", "viewer.auth.email.invalid");
        return normalized;
    }
}

public sealed record ViewerSession(Guid ViewerId, string AgeBracket, string AccessToken, string RefreshToken, int ExpiresInSeconds);
internal sealed record StoredViewerSession(ViewerSession Response, Guid RefreshTokenId, byte[] RefreshTokenHash,
    Guid SessionFamilyId, DateTimeOffset RefreshExpiresAtUtc);
