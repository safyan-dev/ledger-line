using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_audit_logs",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    actor_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    actor_role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_platform_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs_action",
                schema: "audit",
                table: "platform_audit_logs",
                column: "action");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs_actor_id",
                schema: "audit",
                table: "platform_audit_logs",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs_occurred_at",
                schema: "audit",
                table: "platform_audit_logs",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs_target_type_target_id",
                schema: "audit",
                table: "platform_audit_logs",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_platform_audit_logs_tenant_id",
                schema: "audit",
                table: "platform_audit_logs",
                column: "tenant_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_audit_logs",
                schema: "audit");
        }
    }
}
