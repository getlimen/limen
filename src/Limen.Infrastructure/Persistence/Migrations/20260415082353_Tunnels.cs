using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Tunnels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wireguard_peers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TunnelIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wireguard_peers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wireguard_peers_AgentId",
                table: "wireguard_peers",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_wireguard_peers_PublicKey",
                table: "wireguard_peers",
                column: "PublicKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wireguard_peers_TunnelIp",
                table: "wireguard_peers",
                column: "TunnelIp",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wireguard_peers");
        }
    }
}
