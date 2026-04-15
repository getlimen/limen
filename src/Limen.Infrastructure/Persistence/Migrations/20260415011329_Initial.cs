using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limen.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "admin_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                IpAddress = table.Column<string>(type: "text", nullable: true),
                UserAgent = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_sessions", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_admin_sessions_ExpiresAt",
            table: "admin_sessions",
            column: "ExpiresAt");

        migrationBuilder.CreateIndex(
            name: "IX_admin_sessions_Subject",
            table: "admin_sessions",
            column: "Subject");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "admin_sessions");
    }
}
