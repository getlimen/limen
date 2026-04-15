using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limen.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ServicesAndRoutes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "public_routes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                ProxyNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                Hostname = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                TlsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                AuthPolicy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_public_routes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "services",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                TargetNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                ContainerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                InternalPort = table.Column<int>(type: "integer", nullable: false),
                Image = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                AutoDeploy = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_services", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_public_routes_Hostname",
            table: "public_routes",
            column: "Hostname",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_public_routes_ProxyNodeId",
            table: "public_routes",
            column: "ProxyNodeId");

        migrationBuilder.CreateIndex(
            name: "IX_public_routes_ServiceId",
            table: "public_routes",
            column: "ServiceId");

        migrationBuilder.CreateIndex(
            name: "IX_services_Name",
            table: "services",
            column: "Name");

        migrationBuilder.CreateIndex(
            name: "IX_services_TargetNodeId",
            table: "services",
            column: "TargetNodeId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "public_routes");

        migrationBuilder.DropTable(
            name: "services");
    }
}
