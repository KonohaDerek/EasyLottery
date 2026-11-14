using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EasyLotteryCardHub.Data.Migrations
{
    /// <inheritdoc />
    public partial class ViewerAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "viewer_login_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmailHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FailedAttempts = table.Column<int>(type: "integer", nullable: false),
                    ConsumedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_viewer_login_codes", x => x.Id);
                    table.CheckConstraint("ck_viewer_login_codes_failed_attempts", "\"FailedAttempts\" BETWEEN 0 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "viewers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    EmailNormalized = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    TermsVersionAccepted = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TermsAcceptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AgeBracket = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Handle = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    HandleNormalized = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"Handle\")", stored: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_viewers", x => x.Id);
                    table.CheckConstraint("ck_viewers_age_bracket", "\"AgeBracket\" IN ('13_17', '18_plus')");
                });

            migrationBuilder.CreateTable(
                name: "viewer_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NameNormalized = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_viewer_groups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_viewer_groups_viewers_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "viewers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "viewer_refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionFamilyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_viewer_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_viewer_refresh_tokens_viewers_ViewerId",
                        column: x => x.ViewerId,
                        principalTable: "viewers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "viewer_group_items",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CardInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_viewer_group_items", x => new { x.GroupId, x.CardInstanceId });
                    table.ForeignKey(
                        name: "FK_viewer_group_items_viewer_groups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "viewer_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_viewer_group_items_card_instance_id",
                table: "viewer_group_items",
                column: "CardInstanceId");

            migrationBuilder.CreateIndex(
                name: "ix_viewer_groups_viewer_id",
                table: "viewer_groups",
                column: "ViewerId");

            migrationBuilder.CreateIndex(
                name: "ux_viewer_groups_viewer_name",
                table: "viewer_groups",
                columns: new[] { "ViewerId", "NameNormalized" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_viewer_login_codes_expires_at",
                table: "viewer_login_codes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "ux_viewer_login_codes_email_hash",
                table: "viewer_login_codes",
                column: "EmailHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_viewer_refresh_tokens_session_family",
                table: "viewer_refresh_tokens",
                column: "SessionFamilyId");

            migrationBuilder.CreateIndex(
                name: "ix_viewer_refresh_tokens_viewer_expiry",
                table: "viewer_refresh_tokens",
                columns: new[] { "ViewerId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "ux_viewer_refresh_tokens_token_hash",
                table: "viewer_refresh_tokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_viewers_email_normalized",
                table: "viewers",
                column: "EmailNormalized",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_viewers_handle_ci",
                table: "viewers",
                column: "HandleNormalized",
                unique: true,
                filter: "\"HandleNormalized\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "viewer_group_items");

            migrationBuilder.DropTable(
                name: "viewer_login_codes");

            migrationBuilder.DropTable(
                name: "viewer_refresh_tokens");

            migrationBuilder.DropTable(
                name: "viewer_groups");

            migrationBuilder.DropTable(
                name: "viewers");
        }
    }
}
