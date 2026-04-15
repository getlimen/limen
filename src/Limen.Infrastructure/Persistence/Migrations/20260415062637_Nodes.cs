using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limen.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class Nodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "nodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Roles = table.Column<string[]>(type: "text[]", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_nodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "provisioning_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KeyHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IntendedRoles = table.Column<string[]>(type: "text[]", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultingNodeId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_provisioning_keys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretHash = table.Column<byte[]>(type: "bytea", nullable: false),
                    AgentVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Platform = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Hostname = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EnrolledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agents_nodes_NodeId",
                        column: x => x.NodeId,
                        principalTable: "nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_NodeId",
                table: "agents",
                column: "NodeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_nodes_Status",
                table: "nodes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_provisioning_keys_ExpiresAt",
                table: "provisioning_keys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_provisioning_keys_KeyHash",
                table: "provisioning_keys",
                column: "KeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agents");

            migrationBuilder.DropTable(
                name: "provisioning_keys");

            migrationBuilder.DropTable(
                name: "nodes");
        }
}
