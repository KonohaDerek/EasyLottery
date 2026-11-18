using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EasyLotteryCardHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialHubFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ActorType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailsJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "platform_admins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EmailNormalized = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DisabledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_admins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "streamers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EmailNormalized = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PendingDeletionAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streamers", x => x.Id);
                    table.CheckConstraint("ck_streamers_status", "\"Status\" IN ('active', 'disabled', 'pending_deletion', 'deleted')");
                });

            migrationBuilder.CreateTable(
                name: "streamer_invites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EmailNormalized = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByPlatformAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RedeemedByTenantId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streamer_invites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_streamer_invites_platform_admins_CreatedByPlatformAdminId",
                        column: x => x.CreatedByPlatformAdminId,
                        principalTable: "platform_admins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_streamer_invites_streamers_RedeemedByTenantId",
                        column: x => x.RedeemedByTenantId,
                        principalTable: "streamers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "streamer_passkeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "bytea", nullable: false),
                    PublicKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    UserHandle = table.Column<byte[]>(type: "bytea", nullable: false),
                    SignatureCounter = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streamer_passkeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_streamer_passkeys_streamers_TenantId",
                        column: x => x.TenantId,
                        principalTable: "streamers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "streamer_sites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_streamer_sites", x => x.Id);
                    table.ForeignKey(
                        name: "FK_streamer_sites_streamers_TenantId",
                        column: x => x.TenantId,
                        principalTable: "streamers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "site_credentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_site_credentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_site_credentials_streamer_sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "streamer_sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_events_tenant_created_at",
                table: "audit_events",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "ux_platform_admins_email_normalized",
                table: "platform_admins",
                column: "EmailNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_site_credentials_site_id",
                table: "site_credentials",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "ux_site_credentials_secret_hash",
                table: "site_credentials",
                column: "SecretHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_streamer_invites_CreatedByPlatformAdminId",
                table: "streamer_invites",
                column: "CreatedByPlatformAdminId");

            migrationBuilder.CreateIndex(
                name: "ix_streamer_invites_email_expiry",
                table: "streamer_invites",
                columns: new[] { "EmailNormalized", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_streamer_invites_RedeemedByTenantId",
                table: "streamer_invites",
                column: "RedeemedByTenantId");

            migrationBuilder.CreateIndex(
                name: "ux_streamer_invites_token_hash",
                table: "streamer_invites",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_streamer_passkeys_tenant_id",
                table: "streamer_passkeys",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ux_streamer_passkeys_credential_id",
                table: "streamer_passkeys",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_streamer_sites_tenant_id",
                table: "streamer_sites",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "ux_streamer_sites_base_url",
                table: "streamer_sites",
                column: "BaseUrl",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_streamer_sites_client_id",
                table: "streamer_sites",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_streamers_email_normalized",
                table: "streamers",
                column: "EmailNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_streamers_slug",
                table: "streamers",
                column: "Slug",
                unique: true);

            migrationBuilder.Sql("""
                CREATE FUNCTION prevent_audit_events_mutation() RETURNS trigger AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_events is append-only';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER audit_events_append_only
                BEFORE UPDATE OR DELETE ON audit_events
                FOR EACH ROW EXECUTE FUNCTION prevent_audit_events_mutation();

                CREATE TRIGGER audit_events_no_truncate
                BEFORE TRUNCATE ON audit_events
                FOR EACH STATEMENT EXECUTE FUNCTION prevent_audit_events_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_events_append_only ON audit_events;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_events_no_truncate ON audit_events;");
            migrationBuilder.Sql("DROP FUNCTION IF EXISTS prevent_audit_events_mutation();");
            migrationBuilder.DropTable(
                name: "audit_events");

            migrationBuilder.DropTable(
                name: "site_credentials");

            migrationBuilder.DropTable(
                name: "streamer_invites");

            migrationBuilder.DropTable(
                name: "streamer_passkeys");

            migrationBuilder.DropTable(
                name: "streamer_sites");

            migrationBuilder.DropTable(
                name: "platform_admins");

            migrationBuilder.DropTable(
                name: "streamers");
        }
    }
}
