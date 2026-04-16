using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limen.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ResourceAuth : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "allowlisted_emails",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_allowlisted_emails", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "issued_tokens",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                IssuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_issued_tokens", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "magic_links",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_magic_links", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "resource_auth_policies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RouteId = table.Column<Guid>(type: "uuid", nullable: false),
                Mode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                CookieScope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                OidcProviderId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_resource_auth_policies", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_allowlisted_emails_RouteId",
            table: "allowlisted_emails",
            column: "RouteId");

        migrationBuilder.CreateIndex(
            name: "IX_allowlisted_emails_RouteId_Email",
            table: "allowlisted_emails",
            columns: new[] { "RouteId", "Email" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_issued_tokens_ExpiresAt",
            table: "issued_tokens",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_issued_tokens_RevokedAt",
            table: "issued_tokens",
            column: "RevokedAt",
            filter: "\"RevokedAt\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_issued_tokens_Subject",
            table: "issued_tokens",
            column: "Subject");

        migrationBuilder.CreateIndex(
            name: "IX_magic_links_RouteId_Email",
            table: "magic_links",
            columns: new[] { "RouteId", "Email" });

        migrationBuilder.CreateIndex(
            name: "IX_magic_links_TokenHash",
            table: "magic_links",
            column: "TokenHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_resource_auth_policies_RouteId",
            table: "resource_auth_policies",
            column: "RouteId",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "allowlisted_emails");

        migrationBuilder.DropTable(
            name: "issued_tokens");

        migrationBuilder.DropTable(
            name: "magic_links");

        migrationBuilder.DropTable(
            name: "resource_auth_policies");
    }
}
