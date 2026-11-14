using EasyLotteryCardHub.Domain;
using EasyLotteryCardHub.Security;
using Npgsql;
using NpgsqlTypes;

namespace EasyLotteryCardHub.Data;

public sealed record StreamerRecord(Guid Id, string Slug, string DisplayName, TenantStatus Status);
public sealed record SiteRecord(Guid Id, Guid TenantId, string Name, string BaseUrl, string Status);
public sealed record SiteCredentialIssue(Guid Id, string ClientId, string ClientSecret, DateTimeOffset? PreviousSecretExpiresAtUtc = null);
public sealed record AuthenticatedSite(Guid TenantId, Guid SiteId, string ClientId, string TenantName);

public sealed class PostgresCardHubStore(HubDatabase database, TimeProvider timeProvider)
{
    private NpgsqlDataSource DataSource => database.DataSource
        ?? throw new InvalidOperationException("Card Hub database is not configured.");

    public async Task<StreamerRecord> CreateStreamerAsync(string slug, string displayName, CancellationToken cancellationToken)
    {
        var streamer = new StreamerRecord(Guid.NewGuid(), slug, displayName, TenantStatus.Active);
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("INSERT INTO streamers (id, slug, display_name, status) VALUES (@id, @slug, @name, 'active')", connection, transaction);
        command.Parameters.AddWithValue("id", streamer.Id);
        command.Parameters.AddWithValue("slug", streamer.Slug);
        command.Parameters.AddWithValue("name", streamer.DisplayName);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await WriteAuditAsync(connection, transaction, streamer.Id, "platform_admin", "platform-admin", "streamer.created", "streamer", streamer.Id.ToString("D"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return streamer;
    }

    public async Task<bool> SetTenantStatusAsync(Guid tenantId, TenantStatus status, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("UPDATE streamers SET status = @status, pending_deletion_at_utc = CASE WHEN @status = 'pending_deletion' THEN COALESCE(pending_deletion_at_utc, now()) ELSE NULL END, deleted_at_utc = CASE WHEN @status = 'deleted' THEN COALESCE(deleted_at_utc, now()) ELSE NULL END WHERE id = @id AND status <> 'deleted' AND (@status <> 'deleted' OR (status = 'pending_deletion' AND pending_deletion_at_utc <= now() - interval '30 days'))", connection, transaction);
        command.Parameters.AddWithValue("id", tenantId);
        command.Parameters.AddWithValue("status", ToDbStatus(status));
        var updated = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        if (updated)
        {
            await WriteAuditAsync(connection, transaction, tenantId, "platform_admin", "platform-admin", "streamer.status_changed", "streamer", tenantId.ToString("D"), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return updated;
    }

    public async Task<SiteRecord> CreateSiteAsync(Guid tenantId, string name, string baseUrl, CancellationToken cancellationToken)
    {
        var site = new SiteRecord(Guid.NewGuid(), tenantId, name, baseUrl, "active");
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("INSERT INTO streamer_sites (id, tenant_id, name, base_url, status) SELECT @id, id, @name, @base_url, 'active' FROM streamers WHERE id = @tenant_id AND status = 'active'", connection, transaction);
        command.Parameters.AddWithValue("id", site.Id);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("base_url", baseUrl);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new KeyNotFoundException("Streamer tenant not found.");
        await WriteAuditAsync(connection, transaction, tenantId, "platform_admin", "platform-admin", "site.registered", "streamer_site", site.Id.ToString("D"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return site;
    }

    public async Task<SiteCredentialIssue> CreateCredentialAsync(Guid siteId, CancellationToken cancellationToken)
    {
        var (clientId, secret, hash) = SiteSecret.Create();
        var credentialId = Guid.NewGuid();
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("INSERT INTO site_credentials (id, site_id, client_id, secret_hash) SELECT @id, s.id, @client_id, @secret_hash FROM streamer_sites s JOIN streamers t ON t.id = s.tenant_id WHERE s.id = @site_id AND s.status = 'active' AND t.status = 'active'", connection, transaction);
        command.Parameters.AddWithValue("id", credentialId);
        command.Parameters.AddWithValue("site_id", siteId);
        command.Parameters.AddWithValue("client_id", clientId);
        command.Parameters.AddWithValue("secret_hash", hash);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new KeyNotFoundException("Active streamer site not found.");
        await WriteAuditAsync(connection, transaction, null, "platform_admin", "platform-admin", "site.credential_created", "credential", credentialId.ToString("D"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SiteCredentialIssue(credentialId, clientId, secret);
    }

    public async Task<SiteCredentialIssue> RotateCredentialAsync(Guid siteId, CancellationToken cancellationToken, Guid? owningTenantId = null)
    {
        var (clientId, secret, hash) = SiteSecret.Create();
        var credentialId = Guid.NewGuid();
        var expiresAt = timeProvider.GetUtcNow().AddHours(24);
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var retireOldPrevious = new NpgsqlCommand("UPDATE site_credentials SET expires_at_utc = now() WHERE site_id = @site_id AND expires_at_utc IS NOT NULL AND expires_at_utc > now() AND revoked_at_utc IS NULL AND (@tenant_id IS NULL OR EXISTS (SELECT 1 FROM streamer_sites s WHERE s.id = site_credentials.site_id AND s.tenant_id = @tenant_id))", connection, transaction))
        {
            retireOldPrevious.Parameters.AddWithValue("site_id", siteId);
            AddTenantScope(retireOldPrevious, owningTenantId);
            await retireOldPrevious.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var retire = new NpgsqlCommand("UPDATE site_credentials SET expires_at_utc = @previous_expires WHERE site_id = @site_id AND expires_at_utc IS NULL AND revoked_at_utc IS NULL AND (@tenant_id IS NULL OR EXISTS (SELECT 1 FROM streamer_sites s WHERE s.id = site_credentials.site_id AND s.tenant_id = @tenant_id))", connection, transaction))
        {
            retire.Parameters.AddWithValue("site_id", siteId);
            retire.Parameters.AddWithValue("previous_expires", expiresAt);
            AddTenantScope(retire, owningTenantId);
            await retire.ExecuteNonQueryAsync(cancellationToken);
        }
        await using (var insert = new NpgsqlCommand("INSERT INTO site_credentials (id, site_id, client_id, secret_hash) SELECT @id, s.id, @client_id, @secret_hash FROM streamer_sites s JOIN streamers t ON t.id = s.tenant_id WHERE s.id = @site_id AND s.status = 'active' AND t.status = 'active' AND (@tenant_id IS NULL OR s.tenant_id = @tenant_id)", connection, transaction))
        {
            insert.Parameters.AddWithValue("id", credentialId);
            insert.Parameters.AddWithValue("site_id", siteId);
            insert.Parameters.AddWithValue("client_id", clientId);
            insert.Parameters.AddWithValue("secret_hash", hash);
            AddTenantScope(insert, owningTenantId);
            if (await insert.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new KeyNotFoundException("Active streamer site not found.");
        }
        await WriteAuditAsync(connection, transaction, null, "platform_admin", "platform-admin", "site.credential_rotated", "credential", credentialId.ToString("D"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SiteCredentialIssue(credentialId, clientId, secret, expiresAt);
    }

    public async Task<bool> RevokeCredentialAsync(Guid siteId, Guid credentialId, CancellationToken cancellationToken, Guid? owningTenantId = null)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("UPDATE site_credentials c SET revoked_at_utc = COALESCE(c.revoked_at_utc, now()) FROM streamer_sites s WHERE c.id = @id AND c.site_id = @site_id AND s.id = c.site_id AND c.revoked_at_utc IS NULL AND (@tenant_id IS NULL OR s.tenant_id = @tenant_id) RETURNING s.tenant_id", connection, transaction);
        command.Parameters.AddWithValue("id", credentialId);
        command.Parameters.AddWithValue("site_id", siteId);
        AddTenantScope(command, owningTenantId);
        var tenantId = await command.ExecuteScalarAsync(cancellationToken);
        if (tenantId is not Guid tenant) return false;
        await WriteAuditAsync(connection, transaction, tenant, "platform_admin", "platform-admin", "site.credential_revoked", "credential", credentialId.ToString("D"), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<AuthenticatedSite?> AuthenticateSiteAsync(string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT s.tenant_id, s.id, c.secret_hash, t.display_name FROM site_credentials c JOIN streamer_sites s ON s.id = c.site_id JOIN streamers t ON t.id = s.tenant_id WHERE c.client_id = @client_id AND c.revoked_at_utc IS NULL AND (c.expires_at_utc IS NULL OR c.expires_at_utc > @now) AND s.status = 'active' AND t.status = 'active'", connection);
        command.Parameters.AddWithValue("client_id", clientId);
        command.Parameters.AddWithValue("now", timeProvider.GetUtcNow());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var hash = (byte[])reader[2];
            if (SiteSecret.Verify(clientSecret, hash))
                return new AuthenticatedSite(reader.GetGuid(0), reader.GetGuid(1), clientId, reader.GetString(3));
        }
        return null;
    }

    private static string ToDbStatus(TenantStatus status) => status switch
    {
        TenantStatus.Active => "active",
        TenantStatus.Disabled => "disabled",
        TenantStatus.PendingDeletion => "pending_deletion",
        TenantStatus.Deleted => "deleted",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static async Task WriteAuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid? tenantId, string actorType, string actorId, string eventType, string subjectType, string subjectId, CancellationToken cancellationToken)
    {
        await using var audit = new NpgsqlCommand("INSERT INTO audit_events (id, tenant_id, actor_type, actor_id, event_type, subject_type, subject_id) VALUES (@id, @tenant_id, @actor_type, @actor_id, @event_type, @subject_type, @subject_id)", connection, transaction);
        audit.Parameters.AddWithValue("id", Guid.NewGuid());
        audit.Parameters.AddWithValue("tenant_id", (object?)tenantId ?? DBNull.Value);
        audit.Parameters.AddWithValue("actor_type", actorType);
        audit.Parameters.AddWithValue("actor_id", actorId);
        audit.Parameters.AddWithValue("event_type", eventType);
        audit.Parameters.AddWithValue("subject_type", subjectType);
        audit.Parameters.AddWithValue("subject_id", subjectId);
        await audit.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddTenantScope(NpgsqlCommand command, Guid? tenantId) =>
        command.Parameters.Add(new NpgsqlParameter("tenant_id", NpgsqlDbType.Uuid) { Value = (object?)tenantId ?? DBNull.Value });
}
