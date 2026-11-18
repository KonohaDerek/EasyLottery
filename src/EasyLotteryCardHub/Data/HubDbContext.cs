using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EasyLotteryCardHub.Data;

public sealed class HubDbContext(DbContextOptions<HubDbContext> options) : DbContext(options)
{
    public DbSet<StreamerTenant> Streamers => Set<StreamerTenant>();
    public DbSet<StreamerPasskey> StreamerPasskeys => Set<StreamerPasskey>();
    public DbSet<StreamerSite> StreamerSites => Set<StreamerSite>();
    public DbSet<SiteCredential> SiteCredentials => Set<SiteCredential>();
    public DbSet<StreamerInvite> StreamerInvites => Set<StreamerInvite>();
    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var statusConverter = new ValueConverter<TenantStatus, string>(
            status => status.ToStorageValue(),
            value => ParseStatus(value));

        modelBuilder.Entity<StreamerTenant>(entity =>
        {
            entity.ToTable("streamers", table => table.HasCheckConstraint(
                "ck_streamers_status",
                "\"Status\" IN ('active', 'disabled', 'pending_deletion', 'deleted')"));
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasMaxLength(254).IsRequired();
            entity.Property(item => item.EmailNormalized).HasMaxLength(254).IsRequired();
            entity.Property(item => item.DisplayName).HasMaxLength(100).IsRequired();
            entity.Property(item => item.Slug).HasMaxLength(63).IsRequired();
            entity.Property(item => item.Status).HasConversion(statusConverter).HasMaxLength(32).IsRequired();
            entity.HasIndex(item => item.EmailNormalized).IsUnique().HasDatabaseName("ux_streamers_email_normalized");
            entity.HasIndex(item => item.Slug).IsUnique().HasDatabaseName("ux_streamers_slug");
        });

        modelBuilder.Entity<StreamerPasskey>(entity =>
        {
            entity.ToTable("streamer_passkeys");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CredentialId).IsRequired();
            entity.Property(item => item.PublicKey).IsRequired();
            entity.Property(item => item.UserHandle).IsRequired();
            entity.Property(item => item.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => item.CredentialId).IsUnique().HasDatabaseName("ux_streamer_passkeys_credential_id");
            entity.HasIndex(item => item.TenantId).HasDatabaseName("ix_streamer_passkeys_tenant_id");
            entity.HasOne<StreamerTenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StreamerSite>(entity =>
        {
            entity.ToTable("streamer_sites");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Name).HasMaxLength(100).IsRequired();
            entity.Property(item => item.BaseUrl).HasMaxLength(2048).IsRequired();
            entity.Property(item => item.ClientId).HasMaxLength(80).IsRequired();
            entity.HasIndex(item => item.ClientId).IsUnique().HasDatabaseName("ux_streamer_sites_client_id");
            entity.HasIndex(item => item.BaseUrl).IsUnique().HasDatabaseName("ux_streamer_sites_base_url");
            entity.HasIndex(item => item.TenantId).HasDatabaseName("ix_streamer_sites_tenant_id");
            entity.HasOne<StreamerTenant>().WithMany().HasForeignKey(item => item.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SiteCredential>(entity =>
        {
            entity.ToTable("site_credentials");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SecretHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.SecretHash).IsUnique().HasDatabaseName("ux_site_credentials_secret_hash");
            entity.HasIndex(item => item.SiteId).HasDatabaseName("ix_site_credentials_site_id");
            entity.HasOne<StreamerSite>().WithMany().HasForeignKey(item => item.SiteId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StreamerInvite>(entity =>
        {
            entity.ToTable("streamer_invites");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasMaxLength(254).IsRequired();
            entity.Property(item => item.EmailNormalized).HasMaxLength(254).IsRequired();
            entity.Property(item => item.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(item => item.TokenHash).IsUnique().HasDatabaseName("ux_streamer_invites_token_hash");
            entity.HasIndex(item => new { item.EmailNormalized, item.ExpiresAt }).HasDatabaseName("ix_streamer_invites_email_expiry");
            entity.HasOne<PlatformAdmin>().WithMany().HasForeignKey(item => item.CreatedByPlatformAdminId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<StreamerTenant>().WithMany().HasForeignKey(item => item.RedeemedByTenantId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PlatformAdmin>(entity =>
        {
            entity.ToTable("platform_admins");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Email).HasMaxLength(254).IsRequired();
            entity.Property(item => item.EmailNormalized).HasMaxLength(254).IsRequired();
            entity.HasIndex(item => item.EmailNormalized).IsUnique().HasDatabaseName("ux_platform_admins_email_normalized");
        });

        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).UseIdentityAlwaysColumn();
            entity.Property(item => item.EventType).HasMaxLength(120).IsRequired();
            entity.Property(item => item.ActorType).HasMaxLength(40).IsRequired();
            entity.Property(item => item.ActorId).HasMaxLength(160).IsRequired();
            entity.Property(item => item.DetailsJson).HasColumnType("jsonb").IsRequired();
            entity.HasIndex(item => new { item.TenantId, item.CreatedAt }).HasDatabaseName("ix_audit_events_tenant_created_at");
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RejectAuditEventMutation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RejectAuditEventMutation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RejectAuditEventMutation()
    {
        if (ChangeTracker.Entries<AuditEvent>().Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit events are append-only.");
    }

    private static TenantStatus ParseStatus(string value) => TenantStatuses.TryParse(value, out var status)
        ? status
        : throw new InvalidOperationException($"Unknown tenant status '{value}'.");
}
