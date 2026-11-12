using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WebhookIngressReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "conflict_count",
                schema: "webhooks",
                table: "webhook_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "duplicate_count",
                schema: "webhooks",
                table: "webhook_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "last_conflict_reason",
                schema: "webhooks",
                table: "webhook_events",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "payload_hash",
                schema: "webhooks",
                table: "webhook_events",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "provider_order_id",
                schema: "payments",
                table: "payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payments_provider_order_id",
                schema: "payments",
                table: "payments",
                column: "provider_order_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_payments_provider_order_id",
                schema: "payments",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "conflict_count",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "duplicate_count",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "last_conflict_reason",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "payload_hash",
                schema: "webhooks",
                table: "webhook_events");

            migrationBuilder.DropColumn(
                name: "provider_order_id",
                schema: "payments",
                table: "payments");
        }
    }
}
