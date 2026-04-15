using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Limen.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class DeploymentQueue : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "deployments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ServiceId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetNodeId = table.Column<Guid>(type: "uuid", nullable: false),
                ImageDigest = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ImageTag = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                CurrentStage = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Logs = table.Column<string>(type: "text", nullable: false),
                PreviousDeploymentId = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_deployments", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_deployments_ServiceId",
            table: "deployments",
            column: "ServiceId");

        migrationBuilder.CreateIndex(
            name: "IX_deployments_ServiceId_ImageDigest",
            table: "deployments",
            columns: new[] { "ServiceId", "ImageDigest" },
            unique: true,
            filter: "\"Status\" IN ('Queued','InProgress')");

        migrationBuilder.CreateIndex(
            name: "IX_deployments_Status",
            table: "deployments",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_deployments_TargetNodeId",
            table: "deployments",
            column: "TargetNodeId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "deployments");
    }
}
